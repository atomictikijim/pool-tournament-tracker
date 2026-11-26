using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PoolTournamentManager.App.Services;
using PoolTournamentManager.App.ViewModels;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;
using PoolTournamentManager.Data.Persistence;

namespace PoolTournamentManager.App.Tests;

/// <summary>
/// End-to-end check for adding a player to an already-created tournament through the real
/// TournamentViewModel, against a real SQLite file with a long-lived context/repository (as the
/// app's DI wiring produces) - this combination is what surfaced a duplicate-tracked-entity
/// exception from Match.Remove()'s navigation cascade; see TournamentRepository.TrackRemoved.
/// </summary>
public class TournamentEntrantAdditionTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ptm-addentrant-{Guid.NewGuid():N}.db");

    private PoolTournamentDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PoolTournamentDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new PoolTournamentDbContext(options);
    }

    public TournamentEntrantAdditionTests()
    {
        using var ctx = NewContext();
        ctx.Database.Migrate();
    }

    [Fact]
    public async Task AddEntrant_ToSingleElimination_RegeneratesBracketWithoutTrackingError()
    {
        using var ctx = NewContext();
        var tournamentRepo = new TournamentRepository(ctx);
        var playerRepo = new PlayerRepository(ctx);
        var teamRepo = new TeamRepository(ctx);
        var state = new TournamentStateService(tournamentRepo);
        var vm = new TournamentViewModel(
            tournamentRepo, playerRepo, teamRepo,
            new BracketGenerationService(), new RoundRobinSchedulingService(),
            new RingGameService(), new ChipGameService(), state);

        foreach (var name in new[] { "Alice Anderson,700", "Ben Baker,650", "Cara Chen,600", "Dan Diaz,550", "Jane Doe,550" })
        {
            var parts = name.Split(',');
            var full = parts[0].Split(' ');
            await playerRepo.AddAsync(new Player { FirstName = full[0], LastName = full[1], FargoRate = int.Parse(parts[1]) });
        }

        await vm.InitializeAsync();

        vm.NewTournamentName = "Test Single Elim";
        vm.NewTournamentFormat = TournamentFormat.SingleElimination;
        vm.NewTournamentTableCount = 2;
        foreach (var candidate in vm.EntrantCandidates)
        {
            if (candidate.Player.FirstName is "Alice" or "Ben" or "Cara" or "Dan")
            {
                candidate.IsSelected = true;
            }
        }

        await ((IAsyncRelayCommand)vm.CreateTournamentCommand).ExecuteAsync(null);

        Assert.True(vm.CanAddEntrant, $"CanAddEntrant should be true before any match starts. Status: {vm.StatusMessage}");

        vm.SelectedPlayerToAdd = vm.AddablePlayers.First(p => p.FirstName == "Jane");
        await ((IAsyncRelayCommand)vm.AddEntrantCommand).ExecuteAsync(null);

        Assert.DoesNotContain("cannot be tracked", vm.StatusMessage ?? string.Empty);
        Assert.Equal(5, state.ActiveTournament!.Entrants.Count);
        Assert.True(state.ActiveTournament.Bracket!.Nodes.Count > 0);
    }

    /// <summary>
    /// Regression: a first-round bye is materialized as an already-Completed match when the
    /// bracket is generated (BracketGenerationService.MaterializeRound1Match), so treating a
    /// Completed match as "something has been played" disabled Add Entrant for exactly the field
    /// sizes whose bracket contains byes - non-power-of-two Single Elimination (3, 5, ...) and a
    /// 6/7-entrant Modified Single Elimination pod. Only a real match may lock the field.
    /// </summary>
    [Theory]
    [InlineData(TournamentFormat.SingleElimination, 3)]
    [InlineData(TournamentFormat.SingleElimination, 5)]
    [InlineData(TournamentFormat.ModifiedSingleElimination, 6)]
    public async Task CanAddEntrant_StaysTrueWhenTheOnlyCompletedMatchesAreFirstRoundByes(
        TournamentFormat format, int entrantCount)
    {
        using var ctx = NewContext();
        var tournamentRepo = new TournamentRepository(ctx);
        var playerRepo = new PlayerRepository(ctx);
        var teamRepo = new TeamRepository(ctx);
        var state = new TournamentStateService(tournamentRepo);
        var vm = new TournamentViewModel(
            tournamentRepo, playerRepo, teamRepo,
            new BracketGenerationService(), new RoundRobinSchedulingService(),
            new RingGameService(), new ChipGameService(), state);

        // One more player than the field, so there is always someone left to add.
        for (var i = 1; i <= entrantCount + 1; i++)
        {
            await playerRepo.AddAsync(new Player
            {
                FirstName = $"Player{i:00}",
                LastName = "Bye",
                FargoRate = 700 - i
            });
        }

        await vm.InitializeAsync();

        vm.NewTournamentName = $"Byes {format} {entrantCount}";
        vm.NewTournamentFormat = format;
        vm.NewTournamentTableCount = 2;
        foreach (var candidate in vm.EntrantCandidates.Take(entrantCount))
        {
            candidate.IsSelected = true;
        }

        await ((IAsyncRelayCommand)vm.CreateTournamentCommand).ExecuteAsync(null);

        var tournament = state.ActiveTournament;
        Assert.NotNull(tournament);
        Assert.Equal(entrantCount, tournament!.Entrants.Count);

        // The bracket really does contain auto-completed byes - the condition that used to turn
        // CanAddEntrant off before any play - and no real match has started or finished.
        Assert.Contains(tournament.Matches, m => m.IsBye && m.Status == MatchStatus.Completed);
        Assert.DoesNotContain(tournament.Matches,
            m => !m.IsBye && m.Status is MatchStatus.InProgress or MatchStatus.Completed);

        Assert.True(vm.CanAddEntrant,
            $"CanAddEntrant should be true before any real match starts ({format}, {entrantCount} entrants). Status: {vm.StatusMessage}");

        vm.SelectedPlayerToAdd = vm.AddablePlayers.Single();
        await ((IAsyncRelayCommand)vm.AddEntrantCommand).ExecuteAsync(null);

        Assert.Equal(entrantCount + 1, state.ActiveTournament!.Entrants.Count);
        Assert.StartsWith("Added ", vm.StatusMessage);

        // Regenerating at the larger size still only completes byes, so adding must stay available.
        Assert.True(vm.CanAddEntrant,
            $"CanAddEntrant should survive a regeneration that still only has byes complete ({format}, {entrantCount + 1} entrants).");
    }

    [Fact]
    public async Task CreateDoubleElimination_WithNonPowerOfTwoField_WaitlistsOverflowAndSurfacesItInState()
    {
        using var ctx = NewContext();
        var tournamentRepo = new TournamentRepository(ctx);
        var playerRepo = new PlayerRepository(ctx);
        var teamRepo = new TeamRepository(ctx);
        var state = new TournamentStateService(tournamentRepo);
        var vm = new TournamentViewModel(
            tournamentRepo, playerRepo, teamRepo,
            new BracketGenerationService(), new RoundRobinSchedulingService(),
            new RingGameService(), new ChipGameService(), state);

        foreach (var name in new[] { "Al Anderson,700", "Ben Baker,650", "Cara Chen,600", "Dan Diaz,550", "Eve Evans,500" })
        {
            var parts = name.Split(',');
            var full = parts[0].Split(' ');
            await playerRepo.AddAsync(new Player { FirstName = full[0], LastName = full[1], FargoRate = int.Parse(parts[1]) });
        }

        await vm.InitializeAsync();

        vm.NewTournamentName = "Test Double Elim";
        vm.NewTournamentFormat = TournamentFormat.DoubleElimination;
        vm.NewTournamentTableCount = 2;
        foreach (var candidate in vm.EntrantCandidates)
        {
            candidate.IsSelected = true; // all 5
        }

        await ((IAsyncRelayCommand)vm.CreateTournamentCommand).ExecuteAsync(null);

        // 5 entrants -> a 4-player bracket, with the lowest seed (Eve, Fargo 500) waitlisted.
        Assert.Equal(5, state.ActiveTournament!.Entrants.Count);
        Assert.Equal(1, state.ActiveTournament.Entrants.Count(e => e.IsWaitlisted));
        var waitlisted = state.ActiveTournament.Entrants.Single(e => e.IsWaitlisted);
        Assert.Equal("Eve Evans", waitlisted.DisplayName);

        // The waitlist is surfaced to the UI (single name), and the bracket only holds the 4 players.
        Assert.Single(state.Waitlist);
        Assert.Equal("Eve Evans", state.Waitlist[0]);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
        {
            if (File.Exists(f)) { try { File.Delete(f); } catch { } }
        }
    }
}
