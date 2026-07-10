using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.App.ViewModels;

/// <summary>One entrant's row in the "Final Results" panel shown when a tournament completes:
/// finishing placement, name, and the prize their place earned (blank when the place earned
/// nothing). Built from a <see cref="PrizePayoutRow"/> (see PrizePayoutService.ComputeFinalResults),
/// so tied bracket places render as a range (e.g. "3rd-4th").</summary>
public class FinalResultRowViewModel
{
    /// <summary>"1st", "2nd", or "3rd-4th" when this entrant shares a tied place range.</summary>
    public string PlaceDisplay { get; }

    public string EntrantName { get; }

    /// <summary>Formatted prize (e.g. "$50"), or empty when this place earned no prize.</summary>
    public string Payout { get; }

    public bool HasPayout { get; }

    /// <summary>True for the champion's row, so the view can emphasize 1st place.</summary>
    public bool IsChampion { get; }

    public FinalResultRowViewModel(PrizePayoutRow row)
    {
        EntrantName = row.Entrant.DisplayName;
        HasPayout = row.Payout > 0m;
        Payout = HasPayout ? row.Payout.ToString("C0") : string.Empty;
        IsChampion = row.PlaceRangeStart == 1;
        PlaceDisplay = row.PlaceRangeStart == row.PlaceRangeEnd
            ? Ordinal(row.PlaceRangeStart)
            : $"{Ordinal(row.PlaceRangeStart)}-{Ordinal(row.PlaceRangeEnd)}";
    }

    private static string Ordinal(int place) => place switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => $"{place}th"
    };
}
