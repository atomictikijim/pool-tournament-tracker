namespace PoolTournamentManager.Core.Entities;

/// <summary>
/// One recorded game in a chip tournament: the winner beat the loser, and the loser drops one
/// chip. Stored as a simple event log (no per-player chip totals) so chip counts and finishing
/// order can always be replayed from the full sequence - see ChipGameService.
/// </summary>
public class ChipGameEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChipGameDetailId { get; set; }

    public Guid WinnerEntrantId { get; set; }
    public Guid LoserEntrantId { get; set; }

    /// <summary>Monotonic ordering within the tournament, for chronological replay and undo.</summary>
    public int Sequence { get; set; }

    /// <summary>
    /// The table this game was played at. Null for legacy entries recorded before table rotation
    /// was tracked - those still count toward chip loss and win/loss tallies, they just don't
    /// participate in ChipGameService.ComputeTableBoard's seating replay.
    /// </summary>
    public Guid? TableId { get; set; }

    public TournamentEntrant? WinnerEntrant { get; set; }
    public TournamentEntrant? LoserEntrant { get; set; }
    public Table? Table { get; set; }
}
