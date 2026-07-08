using Microsoft.EntityFrameworkCore;
using PoolTournamentManager.App.Services;
using PoolTournamentManager.App.ViewModels;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;
using PoolTournamentManager.Data.Persistence;

namespace PoolTournamentManager.App.Tests;

/// <summary>
/// Proves the Tournament Settings tab's Entrants/Teams checklists filter live via their
/// ICollectionView (the same view WPF's default binding uses), without removing items from the
/// underlying ObservableCollection - so a filtered-out entrant's IsSelected state survives.
/// Uses the same real-SQLite pattern as TournamentEntrantAdditionTests since this depends on the
/// real TournamentViewModel/repository wiring.
/// </summary>
public class EntrantFilteringTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ptm-filtering-{Guid.NewGuid():N}.db");

    private PoolTournamentDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PoolTournamentDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new PoolTournamentDbContext(options);
    }

    public EntrantFilteringTests()
    {
        using var ctx = NewContext();
        ctx.Database.Migrate();
    }

    private static TournamentViewModel NewViewModel(
        TournamentRepository tournamentRepo, PlayerRepository playerRepo, TeamRepository teamRepo, TournamentStateService state) =>
        new(tournamentRepo, playerRepo, teamRepo,
            new BracketGenerationService(), new RoundRobinSchedulingService(),
            new RingGameService(), new ChipGameService(), state);

    [Fact]
    public async Task EntrantNameFilter_HidesNonMatchingPlayers_WithoutClearingSelection()
    {
        using var ctx = NewContext();
        var tournamentRepo = new TournamentRepository(ctx);
        var playerRepo = new PlayerRepository(ctx);
        var teamRepo = new TeamRepository(ctx);
        var state = new TournamentStateService(tournamentRepo);
        var vm = NewViewModel(tournamentRepo, playerRepo, teamRepo, state);

        await playerRepo.AddAsync(new Player { FirstName = "Alice", LastName = "Anderson", FargoRate = 700 });
        await playerRepo.AddAsync(new Player { FirstName = "Bob", LastName = "Baker", FargoRate = 500 });
        await vm.LoadEntrantCandidatesAsync();

        var alice = vm.EntrantCandidates.Single(c => c.Player.FirstName == "Alice");
        alice.IsSelected = true;

        vm.EntrantNameFilter = "ali";

        var visible = vm.EntrantCandidatesView.Cast<PlayerSelectionItem>().ToList();
        Assert.Single(visible);
        Assert.Equal("Alice", visible[0].Player.FirstName);

        // Filtering hides, it doesn't remove - selection and the full collection are untouched.
        Assert.Equal(2, vm.EntrantCandidates.Count);
        Assert.True(alice.IsSelected);
    }

    [Fact]
    public async Task EntrantRatingRange_HidesPlayersOutsideRangeOrWithNoRating()
    {
        using var ctx = NewContext();
        var tournamentRepo = new TournamentRepository(ctx);
        var playerRepo = new PlayerRepository(ctx);
        var teamRepo = new TeamRepository(ctx);
        var state = new TournamentStateService(tournamentRepo);
        var vm = NewViewModel(tournamentRepo, playerRepo, teamRepo, state);

        await playerRepo.AddAsync(new Player { FirstName = "Low", LastName = "Rated", FargoRate = 300 });
        await playerRepo.AddAsync(new Player { FirstName = "Mid", LastName = "Rated", FargoRate = 500 });
        await playerRepo.AddAsync(new Player { FirstName = "High", LastName = "Rated", FargoRate = 700 });
        await playerRepo.AddAsync(new Player { FirstName = "No", LastName = "Rating" });
        await vm.LoadEntrantCandidatesAsync();

        vm.NewTournamentRatingSystem = RatingSystem.Fargo;
        vm.EntrantMinRating = 400;
        vm.EntrantMaxRating = 600;

        var visible = vm.EntrantCandidatesView.Cast<PlayerSelectionItem>().Select(c => c.Player.FirstName).ToList();
        Assert.Equal(new[] { "Mid" }, visible);
    }

    [Fact]
    public async Task TeamFilters_PopulateDistinctOptionsAndFilterByDivisionAndLocation()
    {
        using var ctx = NewContext();
        var tournamentRepo = new TournamentRepository(ctx);
        var playerRepo = new PlayerRepository(ctx);
        var teamRepo = new TeamRepository(ctx);
        var state = new TournamentStateService(tournamentRepo);
        var vm = NewViewModel(tournamentRepo, playerRepo, teamRepo, state);

        await teamRepo.AddAsync(new Team { Name = "Aces", Division = "1", Location = "Corner Pocket" });
        await teamRepo.AddAsync(new Team { Name = "Bulls", Division = "2", Location = "Corner Pocket" });
        await teamRepo.AddAsync(new Team { Name = "Cobras", Division = "1", Location = "Side Pocket" });
        await vm.LoadTeamCandidatesAsync();

        Assert.Equal(new[] { TournamentViewModel.AllFilterOption, "1", "2" }, vm.AvailableDivisionFilters);
        Assert.Equal(new[] { TournamentViewModel.AllFilterOption, "Corner Pocket", "Side Pocket" }, vm.AvailableLocationFilters);

        vm.TeamDivisionFilter = "1";
        var byDivision = vm.TeamCandidatesView.Cast<TeamSelectionItem>().Select(c => c.Team.Name).OrderBy(n => n).ToList();
        Assert.Equal(new[] { "Aces", "Cobras" }, byDivision);

        vm.TeamDivisionFilter = TournamentViewModel.AllFilterOption;
        vm.TeamLocationFilter = "Side Pocket";
        var byLocation = vm.TeamCandidatesView.Cast<TeamSelectionItem>().Select(c => c.Team.Name).ToList();
        Assert.Equal(new[] { "Cobras" }, byLocation);
    }

    [Fact]
    public async Task LoadTeamCandidatesAsync_ResetsDivisionAndLocationFiltersToAll()
    {
        using var ctx = NewContext();
        var tournamentRepo = new TournamentRepository(ctx);
        var playerRepo = new PlayerRepository(ctx);
        var teamRepo = new TeamRepository(ctx);
        var state = new TournamentStateService(tournamentRepo);
        var vm = NewViewModel(tournamentRepo, playerRepo, teamRepo, state);

        await teamRepo.AddAsync(new Team { Name = "Aces", Division = "1", Location = "Corner Pocket" });
        await vm.LoadTeamCandidatesAsync();
        vm.TeamDivisionFilter = "1";
        vm.TeamLocationFilter = "Corner Pocket";

        // Reloading (e.g. clicking "Refresh") rebuilds AvailableDivisionFilters/AvailableLocationFilters,
        // which in the real app resets the bound ComboBox's selection to null - guard against that
        // leaving the filter stuck excluding every team by re-asserting "(All)" every reload.
        await vm.LoadTeamCandidatesAsync();

        Assert.Equal(TournamentViewModel.AllFilterOption, vm.TeamDivisionFilter);
        Assert.Equal(TournamentViewModel.AllFilterOption, vm.TeamLocationFilter);
        Assert.Single(vm.TeamCandidatesView.Cast<TeamSelectionItem>());
    }

    [Fact]
    public async Task TeamNameFilter_IsCaseInsensitiveSubstringMatch()
    {
        using var ctx = NewContext();
        var tournamentRepo = new TournamentRepository(ctx);
        var playerRepo = new PlayerRepository(ctx);
        var teamRepo = new TeamRepository(ctx);
        var state = new TournamentStateService(tournamentRepo);
        var vm = NewViewModel(tournamentRepo, playerRepo, teamRepo, state);

        await teamRepo.AddAsync(new Team { Name = "Aces" });
        await teamRepo.AddAsync(new Team { Name = "Bulls" });
        await vm.LoadTeamCandidatesAsync();

        vm.TeamNameFilter = "ACE";

        var visible = vm.TeamCandidatesView.Cast<TeamSelectionItem>().Select(c => c.Team.Name).ToList();
        Assert.Equal(new[] { "Aces" }, visible);
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
