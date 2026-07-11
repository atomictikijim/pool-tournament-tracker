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
/// The Tournament tab and the read-only Display window must never keep showing a bracket once no
/// tournament is selected, or once the tournament they were showing has just been deleted. Both
/// windows bind their bracket visibility to <c>IsEliminationBracket</c>/<c>ShowFlatRounds</c> and
/// their tree to <c>Bracket</c>, all derived from the shared <see cref="TournamentStateService"/>,
/// so clearing the active tournament must flip those off and empty the tree.
/// </summary>
public class BracketAutoClearTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ptm-bracketclear-{Guid.NewGuid():N}.db");

    private PoolTournamentDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PoolTournamentDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new PoolTournamentDbContext(options);
    }

    public BracketAutoClearTests()
    {
        using var ctx = NewContext();
        ctx.Database.Migrate();
    }

    private static async Task<(TournamentViewModel tournament, DisplayWindowViewModel display, TournamentStateService state)>
        BuildAsync(PoolTournamentDbContext ctx)
    {
        var tournamentRepo = new TournamentRepository(ctx);
        var playerRepo = new PlayerRepository(ctx);
        var teamRepo = new TeamRepository(ctx);
        var state = new TournamentStateService(tournamentRepo);
        var vm = new TournamentViewModel(
            tournamentRepo, playerRepo, teamRepo,
            new BracketGenerationService(), new RoundRobinSchedulingService(),
            new RingGameService(), new ChipGameService(), state);
        var display = new DisplayWindowViewModel(state);

        foreach (var name in new[] { "Alice Anderson", "Ben Baker", "Cara Chen", "Dan Diaz" })
        {
            var parts = name.Split(' ');
            await playerRepo.AddAsync(new Player { FirstName = parts[0], LastName = parts[1] });
        }

        await vm.InitializeAsync();
        return (vm, display, state);
    }

    private static async Task CreateSingleEliminationAsync(TournamentViewModel vm)
    {
        vm.NewTournamentName = "Bracket Clear Test";
        vm.NewTournamentFormat = TournamentFormat.SingleElimination;
        vm.NewTournamentTableCount = 2;
        foreach (var candidate in vm.EntrantCandidates)
        {
            candidate.IsSelected = true;
        }
        await ((IAsyncRelayCommand)vm.CreateTournamentCommand).ExecuteAsync(null);
    }

    [Fact]
    public async Task DeletingTheActiveTournament_ClearsTheBracketOnBothWindows()
    {
        using var ctx = NewContext();
        var (vm, display, _) = await BuildAsync(ctx);
        await CreateSingleEliminationAsync(vm);

        // Sanity: while the tournament is active, both windows show its bracket.
        Assert.True(vm.IsEliminationBracket);
        Assert.NotEmpty(vm.Bracket.Boxes);
        Assert.True(display.IsEliminationBracket);
        Assert.NotEmpty(display.Bracket.Boxes);
        Assert.True(display.HasActiveTournament);

        await vm.DeleteTournamentAsync(vm.State.ActiveTournament!);

        Assert.False(vm.IsEliminationBracket);
        Assert.Empty(vm.Bracket.Boxes);
        Assert.False(display.IsEliminationBracket);
        Assert.Empty(display.Bracket.Boxes);
        // The Display window must also drop the "Now Playing" section (gated on HasActiveTournament),
        // not just the bracket, so it falls back to a clean "No tournament selected" state.
        Assert.False(display.HasActiveTournament);
    }

    [Fact]
    public async Task DeselectingTheTournament_ClearsTheBracketOnBothWindows()
    {
        using var ctx = NewContext();
        var (vm, display, _) = await BuildAsync(ctx);
        await CreateSingleEliminationAsync(vm);

        Assert.True(vm.IsEliminationBracket);
        Assert.True(display.IsEliminationBracket);
        Assert.True(display.HasActiveTournament);

        // Deselecting the list row (SelectedTournamentSummary -> null) must tear the bracket down.
        vm.SelectedTournamentSummary = null;
        // OnSelectedTournamentSummaryChanged kicks off SelectTournamentAsync fire-and-forget; give it a beat.
        await Task.Delay(50);

        Assert.False(vm.IsEliminationBracket);
        Assert.Empty(vm.Bracket.Boxes);
        Assert.False(display.IsEliminationBracket);
        Assert.Empty(display.Bracket.Boxes);
        Assert.False(display.HasActiveTournament);
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
