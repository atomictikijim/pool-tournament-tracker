using Microsoft.EntityFrameworkCore;
using PoolTournamentManager.App.Services;
using PoolTournamentManager.App.ViewModels;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Services;
using PoolTournamentManager.Data.Persistence;

namespace PoolTournamentManager.App.Tests;

/// <summary>
/// Covers the Players/Teams management workflow behind the modal editor + multi-select-delete UI:
/// create/update persistence, deletion of multiple records, and the rule that a player/team still
/// entered in a tournament cannot be deleted (the entrant FK is DeleteBehavior.Restrict). Uses the
/// same real-SQLite wiring as EntrantFilteringTests since the "referenced" check runs a real query.
/// </summary>
public class PlayerTeamManagementTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ptm-mgmt-{Guid.NewGuid():N}.db");

    private PoolTournamentDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PoolTournamentDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new PoolTournamentDbContext(options);
    }

    public PlayerTeamManagementTests()
    {
        using var ctx = NewContext();
        ctx.Database.Migrate();
    }

    private static MainWindowViewModel NewViewModel(PoolTournamentDbContext ctx, out TournamentRepository tournamentRepo)
    {
        var playerRepo = new PlayerRepository(ctx);
        var teamRepo = new TeamRepository(ctx);
        tournamentRepo = new TournamentRepository(ctx);
        var state = new TournamentStateService(tournamentRepo);
        var tournament = new TournamentViewModel(
            tournamentRepo, playerRepo, teamRepo,
            new BracketGenerationService(), new RoundRobinSchedulingService(),
            new RingGameService(), new ChipGameService(), state);
        return new MainWindowViewModel(playerRepo, teamRepo, tournament, new ThemeService());
    }

    [Fact]
    public async Task CreatePlayer_PersistsAndSelectsNewRow()
    {
        using var ctx = NewContext();
        var vm = NewViewModel(ctx, out _);

        var editor = new PlayerEditorViewModel();
        editor.FirstName = "Alice";
        editor.LastName = "Anderson";
        editor.FargoRate = 650;
        await vm.CreatePlayerAsync(editor);

        Assert.Single(vm.Players);
        Assert.Equal("Alice", vm.Players[0].FirstName);
        Assert.Equal(vm.Players[0], vm.SelectedPlayer);
    }

    [Fact]
    public async Task UpdatePlayer_WritesEditedValues()
    {
        using var ctx = NewContext();
        var vm = NewViewModel(ctx, out _);
        await new PlayerRepository(ctx).AddAsync(new Player { FirstName = "Bob", LastName = "Baker" });
        await vm.LoadPlayersAsync();
        var target = vm.Players.Single();

        var editor = new PlayerEditorViewModel();
        editor.LoadFrom(target);
        editor.LastName = "Best";
        await vm.UpdatePlayerAsync(target, editor);

        Assert.Equal("Best", vm.Players.Single().LastName);
    }

    [Fact]
    public async Task DeletePlayers_RemovesMultipleSelectedRows()
    {
        using var ctx = NewContext();
        var vm = NewViewModel(ctx, out _);
        var repo = new PlayerRepository(ctx);
        await repo.AddAsync(new Player { FirstName = "A", LastName = "One" });
        await repo.AddAsync(new Player { FirstName = "B", LastName = "Two" });
        await repo.AddAsync(new Player { FirstName = "C", LastName = "Three" });
        await vm.LoadPlayersAsync();

        var toDelete = vm.Players.Take(2).ToList();
        var survivor = vm.Players[2];
        await vm.DeletePlayersAsync(toDelete);

        Assert.Single(vm.Players);
        Assert.Equal(survivor.Id, vm.Players[0].Id);
    }

    [Fact]
    public async Task DeletePlayers_BlocksPlayerEnteredInTournament()
    {
        using var ctx = NewContext();
        var vm = NewViewModel(ctx, out _);
        var repo = new PlayerRepository(ctx);
        await repo.AddAsync(new Player { FirstName = "Entered", LastName = "Player" });
        await vm.LoadPlayersAsync();
        var player = vm.Players.Single();

        // Enter the player in a tournament so the entrant FK (Restrict) blocks deletion.
        var tournament = new Tournament { Name = "Cup" };
        ctx.Tournaments.Add(tournament);
        ctx.TournamentEntrants.Add(new TournamentEntrant { TournamentId = tournament.Id, PlayerId = player.Id });
        await ctx.SaveChangesAsync();

        await vm.DeletePlayersAsync(new[] { player });

        Assert.Single(vm.Players); // not deleted
        Assert.Contains("Could not delete", vm.StatusMessage);
    }

    [Fact]
    public async Task DeleteTeams_BlocksTeamEnteredInTournament()
    {
        using var ctx = NewContext();
        var vm = NewViewModel(ctx, out _);
        var repo = new TeamRepository(ctx);
        await repo.AddAsync(new Team { Name = "Sharks" });
        await vm.LoadTeamsAsync();
        var team = vm.Teams.Single();

        var tournament = new Tournament { Name = "League" };
        ctx.Tournaments.Add(tournament);
        ctx.TournamentEntrants.Add(new TournamentEntrant { TournamentId = tournament.Id, TeamId = team.Id });
        await ctx.SaveChangesAsync();

        await vm.DeleteTeamsAsync(new[] { team });

        Assert.Single(vm.Teams);
        Assert.Contains("Could not delete", vm.StatusMessage);
    }

    [Fact]
    public void PlayerEditor_TryValidate_ReportsMissingName()
    {
        var editor = new PlayerEditorViewModel();
        editor.Reset();

        Assert.False(editor.TryValidate());
        Assert.NotNull(editor.ErrorMessage);

        editor.FirstName = "Carl";
        editor.LastName = "Cannon";
        Assert.True(editor.TryValidate());
        Assert.Null(editor.ErrorMessage);
    }

    [Fact]
    public void TeamEditor_TryValidate_ReportsMissingName()
    {
        var editor = new TeamEditorViewModel();
        editor.Reset();

        Assert.False(editor.TryValidate());
        Assert.NotNull(editor.ErrorMessage);

        editor.Name = "Cue Crew";
        Assert.True(editor.TryValidate());
        Assert.Null(editor.ErrorMessage);
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
