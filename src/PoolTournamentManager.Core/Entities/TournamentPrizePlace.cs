namespace PoolTournamentManager.Core.Entities;

/// <summary>
/// One configured payout place on a Tournament, e.g. "1st place gets 60% of the prize pool".
/// The number of rows is the number of paid places; percentages across all rows for a
/// tournament must sum to 100.
/// </summary>
public class TournamentPrizePlace
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }

    /// <summary>1-based finishing place, e.g. 1 for 1st place.</summary>
    public int Place { get; set; }

    /// <summary>Percentage (0-100) of the prize pool awarded to this place.</summary>
    public decimal Percentage { get; set; }
}
