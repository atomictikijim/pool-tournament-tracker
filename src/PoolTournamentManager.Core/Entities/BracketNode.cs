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

    public Guid? MatchId { get; set; }
    public Match? Match { get; set; }

    public Guid? FeedsIntoWinnerNodeId { get; set; }
    public Guid? FeedsIntoLoserNodeId { get; set; }
    public bool IsGrandFinalReset { get; set; }
}
