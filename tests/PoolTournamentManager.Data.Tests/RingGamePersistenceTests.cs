using Microsoft.EntityFrameworkCore;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;
using PoolTournamentManager.Data.Persistence;

namespace PoolTournamentManager.Data.Tests;

/// <summary>
/// End-to-end persistence checks for the ring game against a real SQLite file, exercising the same
/// patterns the WPF app uses: AddAsync on an untracked root, TrackNew for entities inserted into an
/// already-tracked aggregate mid-game, and the eager-loading GetByIdAsync round-trip. Each step uses
/// a fresh DbContext so nothing is proven by change-tracker memory alone - it must survive a reload.
/// </summary>
public class RingGamePersistenceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ptm-ringtest-{Guid.NewGuid():N}.db");

    private PoolTournamentDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PoolTournamentDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new PoolTournamentDbContext(options);
    }

    public RingGamePersistenceTests()
    {
        using var ctx = NewContext();
        ctx.Database.Migrate();
    }

    private static TournamentEntrant Entrant(Guid tournamentId, string name) => new()
    {
        TournamentId = tournamentId,
        PlayerId = Guid.NewGuid(),
        Player = new Player { FirstName = name, LastName = "P" }
    };

    [Fact]
    public async Task RingGame_FullFlow_SurvivesReloadAtEveryStep()
    {
        var svc = new RingGameService();

        // --- Create + persist (mirrors TournamentViewModel.CreateTournamentAsync) ---
        var tournamentId = Guid.NewGuid();
        Guid aId, bId, cId;
        {
            using var ctx = NewContext();
            var repo = new TournamentRepository(ctx);

            var t = new Tournament { Id = tournamentId, Name = "Friday Ring", Format = TournamentFormat.RingGame };
            var a = Entrant(t.Id, "Alice");
            var b = Entrant(t.Id, "Bob");
            var c = Entrant(t.Id, "Carol");
            t.Entrants.AddRange(new[] { a, b, c });
            aId = a.Id; bId = b.Id; cId = c.Id;

            // Persist the players so the entrant FK (Restrict) is satisfiable.
            foreach (var e in t.Entrants) { ctx.Add(e.Player!); }

            svc.StartRingGame(t, RingGameType.NineBall, buyInAmount: 20m, fiveBallPayout: 5m, nineBallPayout: 10m);
            await repo.AddAsync(t);
        }

        // Reload: ring detail, buy-ins, rotation, and opening shooter all persisted.
        {
            using var ctx = NewContext();
            var t = await new TournamentRepository(ctx).GetByIdAsync(tournamentId);
            Assert.NotNull(t);
            Assert.Equal(TournamentStatus.InProgress, t!.Status);
            Assert.NotNull(t.RingGame);
            Assert.Equal(3, t.RingGame!.LedgerEntries.Count(l => l.Type == RingLedgerEntryType.BuyIn));
            Assert.Equal(aId, t.RingGame.CurrentShooterEntrantId);
            Assert.Equal(new[] { 1, 2, 3 }, t.Entrants.OrderBy(e => e.SeedNumber).Select(e => e.SeedNumber!.Value));
            Assert.Equal(60m, RingGameService.PotRemaining(t));
        }

        // --- Alice makes the 9: payout + rack advance, persisted via TrackNew (mid-aggregate insert) ---
        {
            using var ctx = NewContext();
            var repo = new TournamentRepository(ctx);
            var t = await repo.GetByIdAsync(tournamentId);
            var entry = svc.RecordMoneyBall(t!, aId, RingMoneyBall.Nine);
            repo.TrackNew(entry);
            await repo.SaveChangesAsync();
        }

        {
            using var ctx = NewContext();
            var t = await new TournamentRepository(ctx).GetByIdAsync(tournamentId);
            Assert.Equal(2, t!.RingGame!.CurrentRackNumber);            // rack advanced
            Assert.Equal(bId, t.RingGame.CurrentShooterEntrantId);      // break rotated to Bob
            var alice = RingGameService.ComputeStandings(t).First(r => r.Entrant.Id == aId);
            Assert.Equal(10m, alice.Winnings);
            Assert.Equal(-10m, alice.Net);                             // -20 buy-in + 10
            Assert.Equal(50m, RingGameService.PotRemaining(t));        // 60 - 10
        }

        // --- Carol cashes out: marker persisted, elimination survives reload ---
        {
            using var ctx = NewContext();
            var repo = new TournamentRepository(ctx);
            var t = await repo.GetByIdAsync(tournamentId);
            var marker = svc.CashOut(t!, cId);
            repo.TrackNew(marker);
            await repo.SaveChangesAsync();
        }

        {
            using var ctx = NewContext();
            var t = await new TournamentRepository(ctx).GetByIdAsync(tournamentId);
            var carol = t!.Entrants.First(e => e.Id == cId);
            Assert.True(carol.IsEliminated);
            Assert.Contains(t.RingGame!.LedgerEntries, l => l.EntrantId == cId && l.Type == RingLedgerEntryType.CashOut);
            Assert.Equal(TournamentStatus.InProgress, t.Status); // Alice + Bob still in
        }
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
        {
            if (File.Exists(f)) { try { File.Delete(f); } catch { /* temp file cleanup is best-effort */ } }
        }
    }
}
