namespace PoolTournamentManager.Core.Entities;

/// <summary>
/// One skill-range → starting-chips rule for a chip tournament (see <see cref="ChipGameDetail"/>).
/// A player whose rating (in the detail's <see cref="ChipGameDetail.ChipRatingSystem"/>) falls in
/// [<see cref="MinRating"/>, <see cref="MaxRating"/>] starts with <see cref="Chips"/> chips. A null
/// bound means "unbounded" on that side, so a rule can express "650 and up" (Max null) or "under
/// 450" (Min null). Rules are evaluated in <see cref="Sequence"/> order and the first match wins;
/// a player with no rating in that system (or matching no rule) falls back to
/// <see cref="ChipGameDetail.StartingChips"/>.
/// </summary>
public class ChipStartingRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChipGameDetailId { get; set; }

    /// <summary>Inclusive lower bound on the rating; null = no lower bound.</summary>
    public int? MinRating { get; set; }

    /// <summary>Inclusive upper bound on the rating; null = no upper bound.</summary>
    public int? MaxRating { get; set; }

    /// <summary>Starting chips (lives) for a player who falls in this range.</summary>
    public int Chips { get; set; }

    /// <summary>Evaluation order; the first matching rule (lowest Sequence) applies.</summary>
    public int Sequence { get; set; }
}
