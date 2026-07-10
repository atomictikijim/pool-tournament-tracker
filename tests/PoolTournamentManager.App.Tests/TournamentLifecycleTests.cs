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
/// End-to-end coverage for the NotStarted tournament lifecycle: a bracket/round-robin tournament
/// now sits at NotStarted (not InProgress) until its first match actually starts, which is also
/// the point past which the bracket can no longer be reshuffled or the tournament's settings
/// edited. Uses the same real-SQLite/real-TournamentViewModel pattern as
/// TournamentEntrantAdditionTests since this depends on the full DI-shaped wiring, not just an
/// isolated Core service.
/// </summary>
public class TournamentLifecycleTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ptm-lifecycle-{Guid.NewGuid():N}.db");

    private PoolTournamentDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PoolTournamentDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new PoolTournamentDbContext(options);
    }

    public TournamentLifecycleTests()
    {
        using var ctx = NewContext();
        ctx.Database.Migrate();
    }

    private static async Task<TournamentViewModel> BuildViewModelWithFourPlayersAsync(PoolTournamentDbContext ctx)
    {
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
        return vm;
    }

    [Fact]
    public async Task CreatingASingleEliminationTournament_LeavesItNotStarted_WithReshuffleAndEditAvailable()
    {
        using var ctx = NewContext();
        var vm = await BuildViewModelWithFourPlayersAsync(ctx);

        vm.NewTournamentName = "Lifecycle Test";
        vm.NewTournamentFormat = TournamentFormat.SingleElimination;
        vm.NewTournamentTableCount = 2;
        foreach (var candidate in vm.EntrantCandidates)
        {
            candidate.IsSelected = true;
        }

        await ((IAsyncRelayCommand)vm.CreateTournamentCommand).ExecuteAsync(null);

        Assert.Equal(TournamentStatus.NotStarted, vm.State.ActiveTournament!.Status);
        Assert.True(vm.CanReshuffleBracket);
        Assert.True(vm.CanEditSelectedTournament);
        // Creating (and saving) auto-selects the tournament and asks the app to switch tabs.
        Assert.Equal("Lifecycle Test", vm.SelectedTournamentSummary?.Name);
    }

    [Fact]
    public async Task StartingTheFirstMatch_FlipsTournamentToInProgress_AndLocksOutReshuffleAndEdit()
    {
        using var ctx = NewContext();
        var vm = await BuildViewModelWithFourPlayersAsync(ctx);

        vm.NewTournamentName = "Lifecycle Test";
        vm.NewTournamentFormat = TournamentFormat.SingleElimination;
        vm.NewTournamentTableCount = 2;
        foreach (var candidate in vm.EntrantCandidates)
        {
            candidate.IsSelected = true;
        }
        await ((IAsyncRelayCommand)vm.CreateTournamentCommand).ExecuteAsync(null);

        var tables = vm.State.ActiveTournament!.Tables;
        var row = vm.State.Rounds.Single(r => r.RoundNumber == 1).Matches.First(m => m.IsStartable);
        row.Match!.TableId = tables[0].Id;
        await ((IAsyncRelayCommand)vm.StartMatchCommand).ExecuteAsync(row);

        Assert.Equal(TournamentStatus.InProgress, vm.State.ActiveTournament!.Status);
        Assert.False(vm.CanReshuffleBracket);
        Assert.False(vm.CanEditSelectedTournament);
    }

    [Fact]
    public async Task ReshuffleBracket_IgnoresRatingSeeding_AndIsBlockedOnceAMatchStarted()
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

        // Distinct, ordered Fargo ratings so rating-based seeding is deterministic and easy to
        // tell apart from a random shuffle.
        foreach (var (name, rate) in new[] { ("Alice Anderson", 900), ("Ben Baker", 800), ("Cara Chen", 700), ("Dan Diaz", 600) })
        {
            var parts = name.Split(' ');
            await playerRepo.AddAsync(new Player { FirstName = parts[0], LastName = parts[1], FargoRate = rate });
        }
        await vm.InitializeAsync();

        vm.NewTournamentName = "Reshuffle Test";
        vm.NewTournamentFormat = TournamentFormat.SingleElimination;
        vm.NewTournamentTableCount = 2;
        foreach (var candidate in vm.EntrantCandidates)
        {
            candidate.IsSelected = true;
        }
        await ((IAsyncRelayCommand)vm.CreateTournamentCommand).ExecuteAsync(null);

        var tournament = vm.State.ActiveTournament!;
        var ratingOrderSeeds = tournament.Entrants
            .OrderBy(e => e.SeedNumber)
            .Select(e => e.Player!.FullName)
            .ToList();
        Assert.Equal(new[] { "Alice Anderson", "Ben Baker", "Cara Chen", "Dan Diaz" }, ratingOrderSeeds);

        // Reshuffling repeatedly must eventually break away from the rating-sorted order - a
        // single reshuffle has a 1-in-24 chance of landing back on the exact same permutation,
        // so retry a few times before concluding the shuffle isn't actually random.
        var brokeRatingOrder = false;
        for (var attempt = 0; attempt < 10 && !brokeRatingOrder; attempt++)
        {
            await ((IAsyncRelayCommand)vm.ReshuffleBracketCommand).ExecuteAsync(null);
            var order = vm.State.ActiveTournament!.Entrants.OrderBy(e => e.SeedNumber).Select(e => e.Player!.FullName).ToList();
            brokeRatingOrder = !order.SequenceEqual(ratingOrderSeeds);
        }
        Assert.True(brokeRatingOrder, "Reshuffle Bracket never produced an order different from rating-based seeding across 10 attempts.");

        // Start the one startable match, then confirm a further reshuffle attempt is a no-op.
        tournament = vm.State.ActiveTournament!;
        var tables = tournament.Tables;
        var row = vm.State.Rounds.Single(r => r.RoundNumber == 1).Matches.First(m => m.IsStartable);
        row.Match!.TableId = tables[0].Id;
        await ((IAsyncRelayCommand)vm.StartMatchCommand).ExecuteAsync(row);

        var seedsBeforeBlockedReshuffle = vm.State.ActiveTournament!.Entrants.OrderBy(e => e.SeedNumber).Select(e => e.Id).ToList();
        await ((IAsyncRelayCommand)vm.ReshuffleBracketCommand).ExecuteAsync(null);
        var seedsAfterBlockedReshuffle = vm.State.ActiveTournament!.Entrants.OrderBy(e => e.SeedNumber).Select(e => e.Id).ToList();
        Assert.Equal(seedsBeforeBlockedReshuffle, seedsAfterBlockedReshuffle);
    }

    [Fact]
    public async Task EditingATournamentsSettings_RebuildsItInPlace_KeepingTheSameId()
    {
        using var ctx = NewContext();
        var vm = await BuildViewModelWithFourPlayersAsync(ctx);

        vm.NewTournamentName = "Edit Test";
        vm.NewTournamentFormat = TournamentFormat.SingleElimination;
        vm.NewTournamentTableCount = 2;
        var playerCandidates = vm.EntrantCandidates.ToList();
        foreach (var candidate in playerCandidates.Take(4))
        {
            candidate.IsSelected = true;
        }
        await ((IAsyncRelayCommand)vm.CreateTournamentCommand).ExecuteAsync(null);

        var originalId = vm.State.ActiveTournament!.Id;

        vm.BeginEditTournament(vm.State.ActiveTournament!);
        Assert.True(vm.IsEditingExistingTournament);
        Assert.Equal("Edit Test", vm.NewTournamentName);
        Assert.Equal(2, vm.NewTournamentTableCount);

        vm.NewTournamentTableCount = 5;
        await ((IAsyncRelayCommand)vm.SaveTournamentSettingsCommand).ExecuteAsync(null);

        Assert.False(vm.IsEditingExistingTournament);
        Assert.Equal(originalId, vm.State.ActiveTournament!.Id);
        Assert.Equal(5, vm.State.ActiveTournament!.Tables.Count);
        Assert.Equal(TournamentStatus.NotStarted, vm.State.ActiveTournament!.Status);
        Assert.Equal(4, vm.State.ActiveTournament!.Entrants.Count);
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
