using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.App.ViewModels;

/// <summary>
/// One player's row in the ring-game rotation/ledger view - carries both their seat position and
/// their running money position, plus the flags the UI uses to highlight whose turn it is and who
/// has cashed out. Rebuilt from a RingStandingRow on every state change.
/// </summary>
public class RingSeatViewModel
{
    public Guid EntrantId { get; }
    public int RotationPosition { get; }
    public string PlayerName { get; }
    public string BuyInDisplay { get; }
    public string WinningsDisplay { get; }
    public string NetDisplay { get; }
    public bool IsCurrentShooter { get; }
    public bool IsCashedOut { get; }

    /// <summary>"1. Alice" style label for the rotation column.</summary>
    public string RotationLabel => $"{RotationPosition}. {PlayerName}";

    /// <summary>Only an active player whose turn it is not is a valid cash-out/next-shot target.</summary>
    public bool CanAct => !IsCashedOut;

    public RingSeatViewModel(RingStandingRow row, Guid? currentShooterEntrantId)
    {
        EntrantId = row.Entrant.Id;
        RotationPosition = row.RotationPosition;
        PlayerName = row.Entrant.Player?.FullName ?? "Unknown";
        BuyInDisplay = row.BuyInTotal.ToString("C0");
        WinningsDisplay = row.Winnings.ToString("C0");
        NetDisplay = row.Net.ToString("C0");
        IsCurrentShooter = !row.IsCashedOut && currentShooterEntrantId == row.Entrant.Id;
        IsCashedOut = row.IsCashedOut;
    }
}
