namespace PoolTournamentManager.Core.Entities;

public class TournamentEntrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }
    public Guid? PlayerId { get; set; }
    public Player? Player { get; set; }
    public Guid? TeamId { get; set; }
    public Team? Team { get; set; }
    public int? SeedNumber { get; set; }
    public bool IsEliminated { get; set; }

    /// <summary>Chip tournaments only: the chips this entrant was given at the start, snapshotted
    /// when the tournament was created/edited (see ChipGameDetail / ChipGameService). Null for
    /// non-chip formats and for chip tournaments created before per-player chips existed, in which
    /// case the flat ChipGameDetail.StartingChips applies.</summary>
    public int? StartingChips { get; set; }

    /// <summary>Chip tournaments only: the tournament director's running adjustment to this
    /// entrant's chip count while the tournament is in progress (a penalty is negative, a bought
    /// chip is positive). Folded into the entrant's chip total on top of <see cref="StartingChips"/>
    /// minus game losses. 0 = no adjustment.</summary>
    public int ChipAdjustment { get; set; }

    public string DisplayName => Player?.FullName ?? Team?.Name ?? "TBD";
}
