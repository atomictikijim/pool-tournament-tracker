using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.Core.Entities;

/// <summary>
/// Ring-game state, attached 1:1 to a Tournament whose Format is RingGame - the ring-game
/// analogue of BracketDetail. Players shoot in a fixed drawn rotation order (stored on each
/// TournamentEntrant.SeedNumber) for the whole session; money moves only via LedgerEntries.
/// </summary>
public class RingGameDetail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }

    /// <summary>The game type: 9-ball or 10-ball ring game.</summary>
    public RingGameType GameType { get; set; } = RingGameType.NineBall;

    /// <summary>The fixed entry fee every player pays to join the ring.</summary>
    public decimal BuyInAmount { get; set; }

    /// <summary>Payout collected by whoever pockets the 5; the rack continues afterward.</summary>
    public decimal FiveBallPayout { get; set; }

    /// <summary>Payout collected by whoever pockets the 9 (or 10 in 10-ball); pocketing it also ends the rack.</summary>
    public decimal NineBallPayout { get; set; }

    /// <summary>1-based rack currently being played.</summary>
    public int CurrentRackNumber { get; set; } = 1;

    /// <summary>The entrant whose turn it currently is (null once the ring is closed).</summary>
    public Guid? CurrentShooterEntrantId { get; set; }

    public List<RingLedgerEntry> LedgerEntries { get; set; } = new();
}
