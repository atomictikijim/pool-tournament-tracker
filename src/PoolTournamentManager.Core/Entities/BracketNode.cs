using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.Core.Entities;

public class BracketNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BracketDetailId { get; set; }
    public BracketSide Side { get; set; }
    public int RoundNumber { get; set; }
    public int PositionInRound { get; set; }

    /// <summary>Entrants that have arrived in this node's two slots so far, before the Match exists.</summary>
    public Guid? Slot1EntrantId { get; set; }
    public Guid? Slot2EntrantId { get; set; }

    /// <summary>
    /// Marks a slot as a permanent bye: no entrant will ever arrive there (the feeder that would
    /// have supplied it was itself a bye). A node with one entrant and one bye auto-completes as a
    /// bye win; a node with both slots byes is a "phantom" that produces no player and propagates a
    /// bye forward. Distinct from a null EntrantId with no bye flag, which means "still pending".
    /// </summary>
    public bool Slot1IsBye { get; set; }
    public bool Slot2IsBye { get; set; }

    /// <summary>True once each slot is either filled by an entrant or known to be a permanent bye.</summary>
    public bool Slot1Resolved => Slot1EntrantId is not null || Slot1IsBye;
    public bool Slot2Resolved => Slot2EntrantId is not null || Slot2IsBye;

    public Guid? MatchId { get; set; }
    public Match? Match { get; set; }

    public Guid? FeedsIntoWinnerNodeId { get; set; }
    public Guid? FeedsIntoLoserNodeId { get; set; }
    public bool IsGrandFinalReset { get; set; }

    /// <summary>
    /// Which slot (1 or 2) this node's winner/loser lands in on its target node. Null falls back
    /// to the legacy PositionInRound-parity convention (even -&gt; slot 1, odd -&gt; slot 2), which is
    /// only unambiguous when a target's two inputs arrive via the same path (both winners, or both
    /// losers). Double-elimination wiring sets these explicitly because a losers-bracket "receiving"
    /// round mixes a winner-path input (the surviving player) with a loser-path input (a freshly
    /// dropped winners-bracket loser) into the same node, where position parity can't disambiguate.
    /// </summary>
    public int? FeedsIntoWinnerSlot { get; set; }
    public int? FeedsIntoLoserSlot { get; set; }
}
