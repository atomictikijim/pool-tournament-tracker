using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.App.ViewModels;

/// <summary>
/// One player's row in the chip-tournament standings: their finishing place (once decided), chip
/// count, and whether they've been eliminated. Rebuilt from a ChipStandingRow on every state
/// change. Also serves the winner/loser pickers (via EntrantId + PlayerName). Payout is shown
/// separately via the generic PrizePayoutRowViewModel panel.
/// </summary>
public class ChipStandingRowViewModel
{
    public Guid EntrantId { get; }
    public string PlayerName { get; }
    public int ChipsRemaining { get; }
    public bool IsEliminated { get; }
    public int? Place { get; }

    /// <summary>"1", "2", … once decided, otherwise a dash.</summary>
    public string PlaceDisplay => Place?.ToString() ?? "—";

    /// <summary>Chip count for the grid, e.g. "3 chips" / "Out".</summary>
    public string ChipsDisplay => IsEliminated ? "Out" : $"{ChipsRemaining} chip{(ChipsRemaining == 1 ? "" : "s")}";

    /// <summary>One-line summary for the read-only display card, e.g. "Place 2  ·  Out".</summary>
    public string SummaryLine
    {
        get
        {
            var parts = new List<string>();
            if (Place is not null) parts.Add($"Place {Place}");
            parts.Add(ChipsDisplay);
            return string.Join("  ·  ", parts);
        }
    }

    public ChipStandingRowViewModel(ChipStandingRow row)
    {
        EntrantId = row.Entrant.Id;
        PlayerName = row.Entrant.Player?.FullName ?? "Unknown";
        ChipsRemaining = row.ChipsRemaining;
        IsEliminated = row.IsEliminated;
        Place = row.Place;
    }
}
