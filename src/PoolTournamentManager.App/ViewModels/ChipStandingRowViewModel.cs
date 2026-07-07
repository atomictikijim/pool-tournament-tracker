using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.App.ViewModels;

/// <summary>
/// One player's row in the chip-tournament standings: their finishing place (once decided), chip
/// count, payout, and whether they've been eliminated. Rebuilt from a ChipStandingRow on every
/// state change. Also serves the winner/loser pickers (via EntrantId + PlayerName).
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

    public string PayoutDisplay { get; }

    /// <summary>Chip count for the grid, e.g. "3 chips" / "Out".</summary>
    public string ChipsDisplay => IsEliminated ? "Out" : $"{ChipsRemaining} chip{(ChipsRemaining == 1 ? "" : "s")}";

    /// <summary>One-line summary for the read-only display card, e.g. "Place 2  ·  Out  ·  $40".</summary>
    public string SummaryLine
    {
        get
        {
            var parts = new List<string>();
            if (Place is not null) parts.Add($"Place {Place}");
            parts.Add(ChipsDisplay);
            if (!string.IsNullOrEmpty(PayoutDisplay)) parts.Add(PayoutDisplay);
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
        PayoutDisplay = row.Payout > 0 ? row.Payout.ToString("C0") : string.Empty;
    }
}
