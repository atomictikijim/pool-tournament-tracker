using Microsoft.EntityFrameworkCore;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;
using PoolTournamentManager.Data.Persistence;

namespace PoolTournamentManager.Data.Tests;

/// <summary>
/// End-to-end persistence checks for the chip tournament against a real SQLite file, exercising the
/// same patterns the WPF app uses: AddAsync on an untracked root at creation, TrackNew for each
/// game entry inserted into an already-tracked aggregate mid-play, and the eager-loading
/// GetByIdAsync round-trip. Each step uses a fresh DbContext so nothing is proven by change-tracker
/// memory alone - chip counts, eliminations, and completion must survive a reload.
/// </summary>
public class ChipGamePersistenceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ptm-chiptest-{Guid.NewGuid():N}.db");

    private PoolTournamentDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PoolTournamentDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new PoolTournamentDbContext(options);
    }

    public ChipGamePersistenceTests()
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
    public async Task ChipTournament_FullFlow_SurvivesReloadAtEveryStep()
    {
        var svc = new ChipGameService();

        // --- Create + persist (mirrors TournamentViewModel.CreateTournamentAsync) ---
        var tournamentId = Guid.NewGuid();
        Guid aId, bId, cId, tableId;
        {
            using var ctx = NewContext();
            var repo = new TournamentRepository(ctx);

            var t = new Tournament { Id = tournamentId, Name = "Chip Night", Format = TournamentFormat.ChipTournament };
            var a = Entrant(t.Id, "Alice");
            var b = Entrant(t.Id, "Bob");
            var c = Entrant(t.Id, "Carol");
            // Pin the table-rotation seed order explicitly (mirrors ShuffleAndSeatPlayers) so the
            // seating below doesn't depend on the Entrants collection's order surviving a reload -
            // EF Core doesn't guarantee that without an explicit OrderBy.
            a.SeedNumber = 1; b.SeedNumber = 2; c.SeedNumber = 3;
            t.Entrants.AddRange(new[] { a, b, c });
            aId = a.Id; bId = b.Id; cId = c.Id;

            var table = new Table { TournamentId = t.Id, Label = "Table 1" };
            t.Tables.Add(table);
            tableId = table.Id;

            foreach (var e in t.Entrants) { ctx.Add(e.Player!); }

            t.EntryFee = 10m;
            svc.StartChipTournament(t, startingChips: 2);
            await repo.AddAsync(t);
        }

        // Reload: chip detail + settings persisted, everyone starts with 2 chips.
        {
            using var ctx = NewContext();
            var t = await new TournamentRepository(ctx).GetByIdAsync(tournamentId);
            Assert.NotNull(t);
            Assert.Equal(TournamentStatus.InProgress, t!.Status);
            Assert.NotNull(t.ChipGame);
            Assert.Equal(2, t.ChipGame!.StartingChips);
            Assert.Equal(30m, PrizePayoutService.TotalEntryFees(t)); // 10 * 3
            Assert.All(ChipGameService.ComputeStandings(t), r => Assert.Equal(2, r.ChipsRemaining));
        }

        // --- Alice beats Bob (mid-aggregate insert via TrackNew) ---
        {
            using var ctx = NewContext();
            var repo = new TournamentRepository(ctx);
            var t = await repo.GetByIdAsync(tournamentId);
            var entry = svc.RecordGame(t!, tableId, winnerId: aId, loserId: bId);
            repo.TrackNew(entry);
            await repo.SaveChangesAsync();
        }

        {
            using var ctx = NewContext();
            var t = await new TournamentRepository(ctx).GetByIdAsync(tournamentId);
            Assert.Single(t!.ChipGame!.Entries);
            Assert.Equal(tableId, t.ChipGame.Entries[0].TableId); // TableId survives the reload
            var standings = ChipGameService.ComputeStandings(t);
            Assert.Equal(1, standings.First(r => r.Entrant.Id == bId).ChipsRemaining); // Bob down one
            Assert.Equal(2, standings.First(r => r.Entrant.Id == aId).ChipsRemaining); // Alice unchanged
            Assert.Equal(TournamentStatus.InProgress, t.Status);
        }

        // Bob (2->1) requeued and Carol rotated onto the table in his place - so the next game
        // at this one-table tournament is Alice vs Carol, not Alice vs Bob again.
        // --- Alice beats Carol; Carol requeues and Bob rotates back in ---
        {
            using var ctx = NewContext();
            var repo = new TournamentRepository(ctx);
            var t = await repo.GetByIdAsync(tournamentId);
            repo.TrackNew(svc.RecordGame(t!, tableId, aId, cId)); // Carol -> 1
            await repo.SaveChangesAsync();
        }

        // --- Alice beats Bob again -> Bob eliminated; Carol (only one left waiting) rotates in ---
        {
            using var ctx = NewContext();
            var repo = new TournamentRepository(ctx);
            var t = await repo.GetByIdAsync(tournamentId);
            repo.TrackNew(svc.RecordGame(t!, tableId, aId, bId)); // Bob -> 0, eliminated
            await repo.SaveChangesAsync();
        }

        {
            using var ctx = NewContext();
            var t = await new TournamentRepository(ctx).GetByIdAsync(tournamentId);
            Assert.True(t!.Entrants.First(e => e.Id == bId).IsEliminated);
            Assert.Equal(TournamentStatus.InProgress, t.Status); // Alice + Carol still in
            Assert.Equal(3, ChipGameService.ComputeStandings(t).First(r => r.Entrant.Id == bId).Place); // first out finishes last
        }

        // --- Alice beats Carol again -> Carol eliminated, Alice champion ---
        {
            using var ctx = NewContext();
            var repo = new TournamentRepository(ctx);
            var t = await repo.GetByIdAsync(tournamentId);
            repo.TrackNew(svc.RecordGame(t!, tableId, aId, cId)); // Carol -> 0, eliminated; Alice champion
            await repo.SaveChangesAsync();
        }

        {
            using var ctx = NewContext();
            var t = await new TournamentRepository(ctx).GetByIdAsync(tournamentId);
            Assert.Equal(TournamentStatus.Completed, t!.Status);
            var standings = ChipGameService.ComputeStandings(t);
            Assert.Equal(aId, standings[0].Entrant.Id);         // champion on top
            Assert.Equal(1, standings[0].Place);
            Assert.Equal(4, t.ChipGame!.Entries.Count);         // all four games persisted
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
