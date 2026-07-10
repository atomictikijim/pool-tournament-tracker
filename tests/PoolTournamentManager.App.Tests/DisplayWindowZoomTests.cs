using Microsoft.EntityFrameworkCore;
using PoolTournamentManager.App.Services;
using PoolTournamentManager.App.ViewModels;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;
using PoolTournamentManager.Data.Persistence;

namespace PoolTournamentManager.App.Tests;

/// <summary>
/// Covers the Display window's zoom gating and fit math after zoom was extended from the
/// elimination bracket to round robin: the zoom controls show for both bracket and round-robin
/// views (but not the ring/chip boards), and FitToViewport scales-to-fit within the clamp range.
/// </summary>
public class DisplayWindowZoomTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ptm-displayzoom-{Guid.NewGuid():N}.db");

    private PoolTournamentDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PoolTournamentDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new PoolTournamentDbContext(options);
    }

    public DisplayWindowZoomTests()
    {
        using var ctx = NewContext();
        ctx.Database.Migrate();
    }

    [Theory]
    [InlineData(TournamentFormat.RoundRobin, true)]
    [InlineData(TournamentFormat.SingleElimination, true)]
    [InlineData(TournamentFormat.DoubleElimination, true)]
    [InlineData(TournamentFormat.RingGame, false)]
    [InlineData(TournamentFormat.ChipTournament, false)]
    public async Task ShowZoomControls_TrueForBracketAndRoundRobin_FalseForRingAndChip(
        TournamentFormat format, bool expected)
    {
        using var ctx = NewContext();
        var repo = new TournamentRepository(ctx);
        var state = new TournamentStateService(repo);
        var tournament = new Tournament { Name = "T", Format = format, Status = TournamentStatus.InProgress };
        await repo.AddAsync(tournament);

        var vm = new DisplayWindowViewModel(state);
        await state.SelectTournamentAsync(tournament.Id);

        Assert.Equal(expected, vm.ShowZoomControls);
    }

    [Fact]
    public async Task RoundRobin_ShowsFlatRounds()
    {
        using var ctx = NewContext();
        var repo = new TournamentRepository(ctx);
        var state = new TournamentStateService(repo);
        var tournament = new Tournament { Name = "RR", Format = TournamentFormat.RoundRobin, Status = TournamentStatus.InProgress };
        await repo.AddAsync(tournament);

        var vm = new DisplayWindowViewModel(state);
        await state.SelectTournamentAsync(tournament.Id);

        Assert.True(vm.ShowFlatRounds);
        Assert.False(vm.IsEliminationBracket);
    }

    [Fact]
    public void FitToViewport_ScalesToFitTheLimitingDimension()
    {
        var state = new TournamentStateService(new TournamentRepository(NewContext()));
        var vm = new DisplayWindowViewModel(state);

        // Height is the limiting dimension: min(500/1000, 500/500) = 0.5.
        vm.FitToViewport(contentWidth: 1000, contentHeight: 500, viewportWidth: 500, viewportHeight: 500);

        Assert.Equal(0.5, vm.BracketZoom, precision: 3);
    }

    [Fact]
    public void FitToViewport_ClampsToTheMinimumZoom()
    {
        var state = new TournamentStateService(new TournamentRepository(NewContext()));
        var vm = new DisplayWindowViewModel(state);

        // A raw fit scale of 0.01 is below the 0.15 floor the +/- buttons respect.
        vm.FitToViewport(contentWidth: 10000, contentHeight: 10000, viewportWidth: 100, viewportHeight: 100);

        Assert.Equal(0.15, vm.BracketZoom, precision: 3);
    }

    [Fact]
    public void FitToViewport_IgnoresNonPositiveInputs()
    {
        var state = new TournamentStateService(new TournamentRepository(NewContext()));
        var vm = new DisplayWindowViewModel(state) { BracketZoom = 1.0 };

        vm.FitToViewport(0, 0, 0, 0);

        Assert.Equal(1.0, vm.BracketZoom, precision: 3);
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
