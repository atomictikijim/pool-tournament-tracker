using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.Core.Services;

/// <summary>A single player's running money position in a ring game, computed from the ledger.</summary>
public class RingStandingRow
{
    public required TournamentEntrant Entrant { get; init; }
    public int RotationPosition { get; init; }
    public decimal BuyInTotal { get; init; }
    public decimal Winnings { get; init; }
    public decimal Net => Winnings - BuyInTotal;
    public bool IsCashedOut { get; init; }
}

/// <summary>
/// Drives a rotation-order ring game and computes its money ledger. Players shoot in a fixed drawn
/// order (TournamentEntrant.SeedNumber, 1-based) for the whole session; a player who cashes out is
/// marked TournamentEntrant.IsEliminated and skipped by the rotation but keeps their ledger history.
///
/// Money is conserved: buy-ins fund a pot, money-ball payouts are drawn from that pot to the
/// shooter, and every player's Net (winnings minus buy-in) sums to the negative of the pot still
/// on the table. Nothing about net/pot is persisted - it is always recomputed from LedgerEntries.
/// </summary>
public class RingGameService
{
    /// <summary>
    /// Sets up a ring game on an already-populated tournament: draws the rotation order from the
    /// entrants' current order (positions 1..N), charges each a buy-in, seats the first player as
    /// the opening breaker, and marks the tournament InProgress.
    /// </summary>
    public RingGameDetail StartRingGame(Tournament tournament, decimal buyInAmount, decimal fiveBallPayout, decimal nineBallPayout)
    {
        if (tournament.Entrants.Count < 2)
        {
            throw new InvalidOperationException("A ring game needs at least 2 players.");
        }

        var detail = new RingGameDetail
        {
            TournamentId = tournament.Id,
            BuyInAmount = buyInAmount,
            FiveBallPayout = fiveBallPayout,
            NineBallPayout = nineBallPayout,
            CurrentRackNumber = 1
        };

        var sequence = 0;
        var position = 1;
        foreach (var entrant in tournament.Entrants)
        {
            entrant.SeedNumber = position++;
            entrant.IsEliminated = false;
            detail.LedgerEntries.Add(new RingLedgerEntry
            {
                RingGameDetailId = detail.Id,
                EntrantId = entrant.Id,
                Type = RingLedgerEntryType.BuyIn,
                Amount = buyInAmount,
                RackNumber = null,
                Sequence = sequence++
            });
        }

        detail.CurrentShooterEntrantId = RotationOrder(tournament).First().Id;
        tournament.RingGame = detail;
        tournament.Status = TournamentStatus.InProgress;
        return detail;
    }

    /// <summary>
    /// Records that <paramref name="shooterEntrantId"/> pocketed a money ball. The 5 pays out and
    /// play continues with the same shooter; the 9 pays out, ends the rack, and rotates the break
    /// to the next active player. Returns the new ledger entry so the caller can persist it.
    /// </summary>
    public RingLedgerEntry RecordMoneyBall(Tournament tournament, Guid shooterEntrantId, RingMoneyBall ball)
    {
        var detail = RequireActiveRing(tournament);
        var shooter = tournament.Entrants.FirstOrDefault(e => e.Id == shooterEntrantId)
            ?? throw new InvalidOperationException("That player is not in this ring game.");
        if (shooter.IsEliminated)
        {
            throw new InvalidOperationException($"{Name(shooter)} has cashed out and can't pocket a ball.");
        }

        var payout = ball == RingMoneyBall.Nine ? detail.NineBallPayout : detail.FiveBallPayout;
        var entry = new RingLedgerEntry
        {
            RingGameDetailId = detail.Id,
            EntrantId = shooter.Id,
            Type = RingLedgerEntryType.MoneyBall,
            Amount = payout,
            RackNumber = detail.CurrentRackNumber,
            Sequence = NextSequence(detail)
        };
        detail.LedgerEntries.Add(entry);

        if (ball == RingMoneyBall.Nine)
        {
            // Rack over: the break rotates to the next active player after the current breaker.
            detail.CurrentRackNumber++;
            detail.CurrentShooterEntrantId = NextActiveAfter(tournament, shooter.Id)?.Id;
        }
        else
        {
            // Made the 5 but not the 9 - the shooter keeps the table.
            detail.CurrentShooterEntrantId = shooter.Id;
        }

        return entry;
    }

    /// <summary>Passes the turn (miss or safety) to the next active player in the rotation.</summary>
    public void AdvanceShooter(Tournament tournament)
    {
        var detail = RequireActiveRing(tournament);
        var current = detail.CurrentShooterEntrantId;
        detail.CurrentShooterEntrantId = current is null
            ? RotationOrder(tournament).FirstOrDefault()?.Id
            : NextActiveAfter(tournament, current.Value)?.Id;
    }

    /// <summary>
    /// Cashes a player out of the ring: marks them eliminated, records a net-neutral CashOut ledger
    /// marker stamped with their realized net, and hands the turn to the next active player if it
    /// was theirs. Returns the marker entry. Completes the tournament once one player remains.
    /// </summary>
    public RingLedgerEntry CashOut(Tournament tournament, Guid entrantId)
    {
        var detail = RequireActiveRing(tournament);
        var entrant = tournament.Entrants.FirstOrDefault(e => e.Id == entrantId)
            ?? throw new InvalidOperationException("That player is not in this ring game.");
        if (entrant.IsEliminated)
        {
            throw new InvalidOperationException($"{Name(entrant)} has already cashed out.");
        }

        var net = ComputeStandings(tournament).First(r => r.Entrant.Id == entrantId).Net;

        var wasCurrent = detail.CurrentShooterEntrantId == entrantId;
        var nextAfter = wasCurrent ? NextActiveAfter(tournament, entrantId) : null;

        entrant.IsEliminated = true;

        var entry = new RingLedgerEntry
        {
            RingGameDetailId = detail.Id,
            EntrantId = entrant.Id,
            Type = RingLedgerEntryType.CashOut,
            Amount = net,
            RackNumber = detail.CurrentRackNumber,
            Sequence = NextSequence(detail)
        };
        detail.LedgerEntries.Add(entry);

        if (wasCurrent)
        {
            detail.CurrentShooterEntrantId = nextAfter?.Id;
        }

        if (tournament.Entrants.Count(e => !e.IsEliminated) <= 1)
        {
            tournament.Status = TournamentStatus.Completed;
            detail.CurrentShooterEntrantId = null;
        }

        return entry;
    }

    /// <summary>
    /// Per-player money standings, computed from the ledger and ordered by net descending. Active
    /// players outrank cashed-out players on a net tie so the current leader board reads naturally.
    /// </summary>
    public static List<RingStandingRow> ComputeStandings(Tournament tournament)
    {
        var ledger = tournament.RingGame?.LedgerEntries ?? new List<RingLedgerEntry>();

        return tournament.Entrants
            .Select(entrant =>
            {
                var entries = ledger.Where(l => l.EntrantId == entrant.Id).ToList();
                return new RingStandingRow
                {
                    Entrant = entrant,
                    RotationPosition = entrant.SeedNumber ?? 0,
                    BuyInTotal = entries.Where(l => l.Type == RingLedgerEntryType.BuyIn).Sum(l => l.Amount),
                    Winnings = entries.Where(l => l.Type == RingLedgerEntryType.MoneyBall).Sum(l => l.Amount),
                    IsCashedOut = entrant.IsEliminated
                };
            })
            .OrderByDescending(r => r.Net)
            .ThenBy(r => r.IsCashedOut)
            .ThenBy(r => r.RotationPosition)
            .ToList();
    }

    /// <summary>Money still on the table: total bought in minus total paid out to shooters.</summary>
    public static decimal PotRemaining(Tournament tournament)
    {
        var ledger = tournament.RingGame?.LedgerEntries ?? new List<RingLedgerEntry>();
        var buyIns = ledger.Where(l => l.Type == RingLedgerEntryType.BuyIn).Sum(l => l.Amount);
        var payouts = ledger.Where(l => l.Type == RingLedgerEntryType.MoneyBall).Sum(l => l.Amount);
        return buyIns - payouts;
    }

    private static RingGameDetail RequireActiveRing(Tournament tournament)
    {
        if (tournament.RingGame is null)
        {
            throw new InvalidOperationException("This tournament is not a ring game.");
        }
        if (tournament.Status == TournamentStatus.Completed)
        {
            throw new InvalidOperationException("This ring game has already finished.");
        }
        return tournament.RingGame;
    }

    private static List<TournamentEntrant> RotationOrder(Tournament tournament) =>
        tournament.Entrants.OrderBy(e => e.SeedNumber ?? int.MaxValue).ToList();

    /// <summary>Next non-cashed-out entrant after the given one, wrapping around the rotation.</summary>
    private static TournamentEntrant? NextActiveAfter(Tournament tournament, Guid entrantId)
    {
        var order = RotationOrder(tournament);
        var startIndex = order.FindIndex(e => e.Id == entrantId);
        if (startIndex < 0)
        {
            return order.FirstOrDefault(e => !e.IsEliminated);
        }

        for (var offset = 1; offset <= order.Count; offset++)
        {
            var candidate = order[(startIndex + offset) % order.Count];
            if (!candidate.IsEliminated)
            {
                return candidate;
            }
        }
        return null;
    }

    private static int NextSequence(RingGameDetail detail) =>
        detail.LedgerEntries.Count == 0 ? 0 : detail.LedgerEntries.Max(l => l.Sequence) + 1;

    private static string Name(TournamentEntrant entrant) => entrant.Player?.FullName ?? "That player";
}
