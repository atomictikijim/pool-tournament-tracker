using Microsoft.EntityFrameworkCore;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;
using PoolTournamentManager.Data.Persistence;

namespace PoolTournamentManager.Data.Tests;

/// <summary>
/// Proves TournamentRepository.DeleteAsync removes a tournament and every owned child row
/// (entrants, tables, matches, bracket detail + nodes) against real SQLite, WITHOUT deleting the
/// Players its entrants referenced. The delete path is exercised on a single shared DbContext that
/// has already eager-loaded the tournament graph (as the running app does when a tournament is
/// selected), since that is exactly the tracked-graph scenario where an EF cascade delete is most
/// likely to trip over the tournament's internal Restrict foreign keys or a duplicate-identity walk.
/// </summary>
public class TournamentDeletionTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ptm-deltest-{Guid.NewGuid():N}.db");

    private PoolTournamentDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PoolTournamentDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new PoolTournamentDbContext(options);
    }

    public TournamentDeletionTests()
    {
        using var ctx = NewContext();
        ctx.Database.Migrate();
    }

    [Fact]
    public async Task DeleteAsync_RemovesTournamentAndAllOwnedRows_ButKeepsPlayers()
    {
        var playerIds = new List<Guid>();
        var tournamentId = Guid.NewGuid();

        // --- Create a single-elimination tournament with a real bracket (mirrors CreateTournamentAsync) ---
        {
            using var ctx = NewContext();
            var repo = new TournamentRepository(ctx);
            var t = new Tournament { Id = tournamentId, Name = "To Delete", Format = TournamentFormat.SingleElimination };
            for (var i = 0; i < 4; i++)
            {
                var player = new Player { FirstName = $"P{i}", LastName = "Player", FargoRate = 500 + i };
                ctx.Add(player);
                playerIds.Add(player.Id);
                t.Entrants.Add(new TournamentEntrant { TournamentId = t.Id, PlayerId = player.Id, Player = player });
            }
            t.Tables.Add(new Table { TournamentId = t.Id, Label = "Table 1" });
            SeedingService.AssignSeeds(t.Entrants, RatingSystem.Fargo);
            new BracketGenerationService().GenerateSingleElimination(t);
            await repo.AddAsync(t);
        }

        // Sanity: children exist before the delete.
        using (var ctx = NewContext())
        {
            Assert.Equal(1, await ctx.Tournaments.CountAsync());
            Assert.Equal(4, await ctx.TournamentEntrants.CountAsync());
            Assert.True(await ctx.BracketNodes.CountAsync() > 0);
        }

        // --- Delete on a shared context that first selects (eager-loads + tracks) the tournament ---
        using (var ctx = NewContext())
        {
            var repo = new TournamentRepository(ctx);
            await repo.GetByIdAsync(tournamentId); // tracks the full graph, like selecting it in the app
            await repo.DeleteAsync(tournamentId);
        }

        // --- Everything owned is gone; the players survive ---
        using (var ctx = NewContext())
        {
            Assert.Equal(0, await ctx.Tournaments.CountAsync());
            Assert.Equal(0, await ctx.TournamentEntrants.CountAsync());
            Assert.Equal(0, await ctx.Tables.CountAsync());
            Assert.Equal(0, await ctx.Matches.CountAsync());
            Assert.Equal(0, await ctx.BracketDetails.CountAsync());
            Assert.Equal(0, await ctx.BracketNodes.CountAsync());
            Assert.Equal(4, await ctx.Players.CountAsync());
            foreach (var id in playerIds)
            {
                Assert.NotNull(await ctx.Players.FindAsync(id));
            }
        }
    }

    [Fact]
    public async Task DeleteAsync_IsNoOp_ForUnknownId()
    {
        using var ctx = NewContext();
        var repo = new TournamentRepository(ctx);
        await repo.DeleteAsync(Guid.NewGuid()); // must not throw
        Assert.Equal(0, await ctx.Tournaments.CountAsync());
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
