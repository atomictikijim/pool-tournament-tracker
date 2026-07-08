using Microsoft.EntityFrameworkCore;
using PoolTournamentManager.App.Services;
using PoolTournamentManager.App.ViewModels;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;
using PoolTournamentManager.Data.Persistence;

namespace PoolTournamentManager.App.Tests;

/// <summary>
/// Proves the Tournament tab's status filter hides rows through the live ICollectionView (the same
/// view the ListBox binds) by tournament status, without removing anything from the underlying
/// State.Tournaments collection. Uses the real SQLite-backed TournamentViewModel wiring, like
/// EntrantFilteringTests.
/// </summary>
public class TournamentStatusFilterTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ptm-statusfilter-{Guid.NewGuid():N}.db");

    private PoolTournamentDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PoolTournamentDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new PoolTournamentDbContext(options);
    }

    public TournamentStatusFilterTests()
    {
        using var ctx = NewContext();
        ctx.Database.Migrate();
    }

    private static TournamentViewModel NewViewModel(PoolTournamentDbContext ctx, out TournamentStateService state)
    {
        var tournamentRepo = new TournamentRepository(ctx);
        var playerRepo = new PlayerRepository(ctx);
        var teamRepo = new TeamRepository(ctx);
        state = new TournamentStateService(tournamentRepo);
        return new TournamentViewModel(tournamentRepo, playerRepo, teamRepo,
            new BracketGenerationService(), new RoundRobinSchedulingService(),
            new RingGameService(), new ChipGameService(), state);
    }

    private async Task SeedAsync(TournamentRepository repo)
    {
        await repo.AddAsync(new Tournament { Name = "Live One", Format = TournamentFormat.SingleElimination, Status = TournamentStatus.InProgress });
        await repo.AddAsync(new Tournament { Name = "Live Two", Format = TournamentFormat.RoundRobin, Status = TournamentStatus.InProgress });
        await repo.AddAsync(new Tournament { Name = "Done One", Format = TournamentFormat.SingleElimination, Status = TournamentStatus.Completed });
    }

    [Fact]
    public async Task Filter_All_ShowsEveryTournament()
    {
        using var ctx = NewContext();
        var vm = NewViewModel(ctx, out var state);
        await SeedAsync(new TournamentRepository(ctx));
        await state.LoadTournamentsAsync();

        vm.TournamentStatusFilter = TournamentViewModel.StatusFilterAll;

        Assert.Equal(3, vm.TournamentsView.Cast<Tournament>().Count());
        Assert.Equal(3, state.Tournaments.Count); // underlying collection never trimmed
    }

    [Fact]
    public async Task Filter_InProgress_ShowsOnlyInProgress()
    {
        using var ctx = NewContext();
        var vm = NewViewModel(ctx, out var state);
        await SeedAsync(new TournamentRepository(ctx));
        await state.LoadTournamentsAsync();

        vm.TournamentStatusFilter = TournamentViewModel.StatusFilterInProgress;

        var visible = vm.TournamentsView.Cast<Tournament>().ToList();
        Assert.Equal(2, visible.Count);
        Assert.All(visible, t => Assert.Equal(TournamentStatus.InProgress, t.Status));
        Assert.Equal(3, state.Tournaments.Count);
    }

    [Fact]
    public async Task Filter_Completed_ShowsOnlyCompleted()
    {
        using var ctx = NewContext();
        var vm = NewViewModel(ctx, out var state);
        await SeedAsync(new TournamentRepository(ctx));
        await state.LoadTournamentsAsync();

        vm.TournamentStatusFilter = TournamentViewModel.StatusFilterCompleted;

        var visible = vm.TournamentsView.Cast<Tournament>().ToList();
        Assert.Single(visible);
        Assert.Equal("Done One", visible[0].Name);
    }

    [Fact]
    public async Task Filter_SurvivesReload_AndRevertsToAll()
    {
        using var ctx = NewContext();
        var vm = NewViewModel(ctx, out var state);
        await SeedAsync(new TournamentRepository(ctx));
        await state.LoadTournamentsAsync();

        vm.TournamentStatusFilter = TournamentViewModel.StatusFilterCompleted;
        await state.LoadTournamentsAsync(); // e.g. after a delete/refresh
        Assert.Single(vm.TournamentsView.Cast<Tournament>()); // filter still applied after reload

        vm.TournamentStatusFilter = TournamentViewModel.StatusFilterAll;
        Assert.Equal(3, vm.TournamentsView.Cast<Tournament>().Count());
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
