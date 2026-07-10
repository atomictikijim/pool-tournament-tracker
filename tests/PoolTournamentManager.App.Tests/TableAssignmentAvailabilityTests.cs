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
/// Proves the table-picker ComboBox's ItemsSource (MatchRowViewModel.AvailableTables) excludes
/// whichever table an in-progress match is currently occupying, and always lists the rest in
/// numerical order regardless of how the tables happen to come back from the database - not
/// whatever order Table.Id/insertion happens to produce. Uses the same real-SQLite pattern as
/// BracketFullVisibilityTests since this depends on the real TournamentViewModel/
/// TournamentStateService/RebuildRounds wiring, not just an isolated unit.
/// </summary>
public class TableAssignmentAvailabilityTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ptm-tableavail-{Guid.NewGuid():N}.db");

    private PoolTournamentDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PoolTournamentDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new PoolTournamentDbContext(options);
    }

    public TableAssignmentAvailabilityTests()
    {
        using var ctx = NewContext();
        ctx.Database.Migrate();
    }

    [Fact]
    public async Task StartingAMatch_HidesItsTable_AndLeavesTheRestInNumericalOrder()
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

        vm.NewTournamentName = "Test Table Availability";
        vm.NewTournamentFormat = TournamentFormat.SingleElimination;
        vm.NewTournamentTableCount = 4;
        foreach (var candidate in vm.EntrantCandidates)
        {
            candidate.IsSelected = true;
        }

        await ((IAsyncRelayCommand)vm.CreateTournamentCommand).ExecuteAsync(null);

        var round1 = state.Rounds.Single(r => r.RoundNumber == 1);
        Assert.Equal(2, round1.Matches.Count);

        // Every table is open before anything starts, and already listed in numerical order.
        var firstRow = round1.Matches[0];
        Assert.Equal(
            new[] { "Table 1", "Table 2", "Table 3", "Table 4" },
            firstRow.AvailableTables.Select(t => t.Label));

        // Start the first match on "Table 3" specifically (not first or last in the list), so a
        // naive "hide whatever was last assigned" bug wouldn't accidentally pass this check.
        var table3 = state.ActiveTournament!.Tables.Single(t => t.Label == "Table 3");
        firstRow.Match!.TableId = table3.Id;
        await ((IAsyncRelayCommand)vm.StartMatchCommand).ExecuteAsync(firstRow);

        var secondRow = state.Rounds.Single(r => r.RoundNumber == 1).Matches.Single(m => m.IsStartable);
        Assert.Equal(
            new[] { "Table 1", "Table 2", "Table 4" },
            secondRow.AvailableTables.Select(t => t.Label));
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
