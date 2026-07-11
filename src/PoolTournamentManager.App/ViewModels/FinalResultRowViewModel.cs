using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.App.ViewModels;

/// <summary>One entrant's row in the "Final Results" panel shown when a tournament completes:
/// finishing placement, name, and the prize their place earned (blank when the place earned
/// nothing). Built from a <see cref="PrizePayoutRow"/> (see PrizePayoutService.ComputeFinalResults),
/// so tied bracket places render as a range (e.g. "3rd-4th"). Modified Single Elimination instead
/// uses <see cref="Qualifier"/>, listing each independent bracket's winner as "Qualified".</summary>
public class FinalResultRowViewModel
{
    /// <summary>"1st", "2nd", or "3rd-4th" when this entrant shares a tied place range; "Qualified"
    /// for a Modified Single Elimination bracket winner.</summary>
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

    private FinalResultRowViewModel(string placeDisplay, string entrantName, bool isChampion)
    {
        PlaceDisplay = placeDisplay;
        EntrantName = entrantName;
        IsChampion = isChampion;
        Payout = string.Empty;
        HasPayout = false;
    }

    /// <summary>A row for a Modified Single Elimination bracket winner - one of the tournament's
    /// several co-equal winners, each shown as "Qualified" with no prize (it's a qualifier format).</summary>
    public static FinalResultRowViewModel Qualifier(TournamentEntrant entrant) =>
        new("Qualified", entrant.DisplayName, isChampion: true);

    private static string Ordinal(int place) => place switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => $"{place}th"
    };
}
