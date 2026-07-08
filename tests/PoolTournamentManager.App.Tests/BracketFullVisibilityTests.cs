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
/// Proves the whole bracket renders from tournament creation - not just the currently-playable
/// round - by checking the Final round shows a "TBD vs TBD" placeholder before Round 1 is played,
/// then real winner names once Round 1 completes. Uses the same real-SQLite pattern as
/// TournamentEntrantAdditionTests since this depends on the real TournamentViewModel/
/// TournamentStateService wiring, not just BracketGenerationService in isolation.
/// </summary>
public class BracketFullVisibilityTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ptm-fullbracket-{Guid.NewGuid():N}.db");

    private PoolTournamentDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PoolTournamentDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new PoolTournamentDbContext(options);
    }

    public BracketFullVisibilityTests()
    {
        using var ctx = NewContext();
        ctx.Database.Migrate();
    }

    [Fact]
    public async Task SingleElimination_ShowsFinalAsPlaceholder_ThenFillsInWinnersAfterRound1()
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

        foreach (var name in new[] { "Alice Anderson", "Ben Baker", "Cara Chen", "Dan Diaz" })
        {
            var parts = name.Split(' ');
            await playerRepo.AddAsync(new Player { FirstName = parts[0], LastName = parts[1] });
        }

        await vm.InitializeAsync();

        vm.NewTournamentName = "Test Full Bracket Visibility";
        vm.NewTournamentFormat = TournamentFormat.SingleElimination;
        vm.NewTournamentTableCount = 2;
        foreach (var candidate in vm.EntrantCandidates)
        {
            candidate.IsSelected = true;
        }

        await ((IAsyncRelayCommand)vm.CreateTournamentCommand).ExecuteAsync(null);

        // Before any match is played: the Final round already exists as a placeholder.
        var finalRound = state.Rounds.Single(r => r.RoundNumber == 2);
        Assert.Equal("Final", finalRound.Title);
        var finalRow = Assert.Single(finalRound.Matches);
        Assert.True(finalRow.IsPlaceholder);
        Assert.Equal("TBD", finalRow.Player1Name);
        Assert.Equal("TBD", finalRow.Player2Name);

        // Play both Round 1 matches through Start/Finish. Re-fetch the round from state.Rounds
        // on each iteration rather than reusing one snapshot - FinishMatchAsync reloads the whole
        // tournament graph from the database, which replaces every MatchRowViewModel/Match
        // instance, so a snapshot taken before the first Finish would be stale for the second.
        Assert.Equal(2, state.Rounds.Single(r => r.RoundNumber == 1).Matches.Count);
        var tables = state.ActiveTournament!.Tables;

        for (var i = 0; i < 2; i++)
        {
            var row = state.Rounds.Single(r => r.RoundNumber == 1).Matches.First(m => m.IsStartable);
            row.Match!.TableId = tables[i].Id;
            await ((IAsyncRelayCommand)vm.StartMatchCommand).ExecuteAsync(row);
            row.Match!.Player1Score = 5;
            row.Match!.Player2Score = 1;
            await ((IAsyncRelayCommand)vm.FinishMatchCommand).ExecuteAsync(row);
        }

        // After Round 1 completes, the Final round's placeholder resolved to real winner names.
        var finalRoundAfter = state.Rounds.Single(r => r.RoundNumber == 2);
        var finalRowAfter = Assert.Single(finalRoundAfter.Matches);
        Assert.False(finalRowAfter.IsPlaceholder);
        Assert.NotEqual("TBD", finalRowAfter.Player1Name);
        Assert.NotEqual("TBD", finalRowAfter.Player2Name);
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
