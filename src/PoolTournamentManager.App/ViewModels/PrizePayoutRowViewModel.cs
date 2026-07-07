using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.App.ViewModels;

/// <summary>One entrant's row in the shared "Prize Payouts" panel (Tournament tab and Display
/// window), used identically for Round Robin, Chip Tournament, and elimination brackets.</summary>
public class PrizePayoutRowViewModel
{
    public string EntrantName { get; }
    public string Payout { get; }

    /// <summary>"1st", "2nd", or "3rd-4th (tied)" when this entrant shares a place range.</summary>
    public string PlaceDisplay { get; }

    public PrizePayoutRowViewModel(PrizePayoutRow row)
    {
        EntrantName = row.Entrant.DisplayName;
        Payout = row.Payout.ToString("C0");
        PlaceDisplay = row.PlaceRangeStart == row.PlaceRangeEnd
            ? Ordinal(row.PlaceRangeStart)
            : $"{Ordinal(row.PlaceRangeStart)}-{Ordinal(row.PlaceRangeEnd)} (tied)";
    }

    private static string Ordinal(int place) => place switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => $"{place}th"
    };
}
