using Microsoft.EntityFrameworkCore;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Data.Persistence;

namespace PoolTournamentManager.Data.Tests;

/// <summary>
/// The Tournament tab's picker lists tournaments newest first. Tournament.Id is a random v4 GUID, so
/// ordering by it produced an arbitrary (though stable) list in which a freshly created tournament
/// could land anywhere - these tests pin GetAllAsync to the real creation timestamp instead, against
/// a real SQLite file so the DateTime round-trip and ordering happen in the database, not in memory.
/// </summary>
public class TournamentOrderingTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ptm-order-{Guid.NewGuid():N}.db");

    private PoolTournamentDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PoolTournamentDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new PoolTournamentDbContext(options);
    }

    public TournamentOrderingTests()
    {
        using var ctx = NewContext();
        ctx.Database.Migrate();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsNewestCreatedFirst()
    {
        var oldest = new Tournament
        {
            Name = "Oldest",
            Format = TournamentFormat.RoundRobin,
            CreatedAtUtc = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)
        };
        var middle = new Tournament
        {
            Name = "Middle",
            Format = TournamentFormat.SingleElimination,
            CreatedAtUtc = new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc)
        };
        var newest = new Tournament
        {
            Name = "Newest",
            Format = TournamentFormat.DoubleElimination,
            CreatedAtUtc = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc)
        };

        // Insert in neither creation order nor reverse creation order, so neither insertion order
        // nor a random-GUID ordering can make this pass by accident.
        using (var ctx = NewContext())
        {
            var repo = new TournamentRepository(ctx);
            await repo.AddAsync(middle);
            await repo.AddAsync(oldest);
            await repo.AddAsync(newest);
        }

        using (var ctx = NewContext())
        {
            var repo = new TournamentRepository(ctx);
            var names = (await repo.GetAllAsync()).Select(t => t.Name).ToArray();

            Assert.Equal(new[] { "Newest", "Middle", "Oldest" }, names);
        }
    }

    [Fact]
    public async Task GetAllAsync_WithIdenticalTimestamps_IsStableBetweenLoads()
    {
        var sameTime = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        using (var ctx = NewContext())
        {
            var repo = new TournamentRepository(ctx);
            foreach (var name in new[] { "Alpha", "Bravo", "Charlie" })
            {
                await repo.AddAsync(new Tournament { Name = name, CreatedAtUtc = sameTime });
            }
        }

        async Task<string[]> LoadNamesAsync()
        {
            using var ctx = NewContext();
            return (await new TournamentRepository(ctx).GetAllAsync()).Select(t => t.Name).ToArray();
        }

        // The Id tie-break exists so tournaments that share a timestamp (e.g. the rows backfilled by
        // the AddTournamentCreatedAt migration) never shuffle between loads.
        Assert.Equal(await LoadNamesAsync(), await LoadNamesAsync());
    }

    [Fact]
    public void NewTournament_DefaultsToUtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var tournament = new Tournament { Name = "Fresh" };
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.InRange(tournament.CreatedAtUtc, before, after);
        Assert.Equal(DateTimeKind.Utc, tournament.CreatedAtUtc.Kind);
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
