using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.Core.Entities;

/// <summary>
/// One money event in a ring game: a buy-in, a money-ball payout, or a cash-out marker. Amount is
/// always stored as a non-negative magnitude; its effect on a player's net is derived from Type
/// (BuyIn subtracts, MoneyBall adds, CashOut is a net-neutral record of the moment they left).
/// </summary>
public class RingLedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RingGameDetailId { get; set; }
    public Guid EntrantId { get; set; }

    public RingLedgerEntryType Type { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Rack this event happened on (null for buy-ins recorded before play starts).</summary>
    public int? RackNumber { get; set; }

    /// <summary>Monotonic ordering within the ring, for chronological display and undo.</summary>
    public int Sequence { get; set; }

    public TournamentEntrant? Entrant { get; set; }
}
