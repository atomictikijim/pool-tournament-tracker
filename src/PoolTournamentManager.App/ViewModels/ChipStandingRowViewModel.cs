using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.App.ViewModels;

/// <summary>The (entrant, delta) pair submitted when the director clicks a +/- chip button in the
/// chip-tournament standings - the single parameter type for AdjustChipsCommand.</summary>
public record ChipAdjustmentRequest(Guid EntrantId, int Delta);

/// <summary>
/// One player's row in the chip-tournament standings: their finishing place (once decided), chip
/// count, win/loss record, and whether they've been eliminated. Rebuilt from a ChipStandingRow on
/// every state change. Payout is shown separately via the generic PrizePayoutRowViewModel panel.
/// </summary>
public class ChipStandingRowViewModel
{
    public Guid EntrantId { get; }
    public string PlayerName { get; }
    public int ChipsRemaining { get; }
    public bool IsEliminated { get; }
    public int? Place { get; }
    public int MatchesWon { get; }
    public int MatchesPlayed { get; }
    public double WinPercentage { get; }

    /// <summary>"1", "2", … once decided, otherwise a dash.</summary>
    public string PlaceDisplay => Place?.ToString() ?? "—";

    /// <summary>Chip count for the grid, e.g. "3 chips" / "Out".</summary>
    public string ChipsDisplay => IsEliminated ? "Out" : $"{ChipsRemaining} chip{(ChipsRemaining == 1 ? "" : "s")}";

    /// <summary>Win-loss record for the grid, e.g. "5-2".</summary>
    public string RecordDisplay => $"{MatchesWon}-{MatchesPlayed - MatchesWon}";

    /// <summary>Win percentage for the grid, e.g. "71%", or a dash before anyone's played.</summary>
    public string WinPercentageDisplay => MatchesPlayed == 0 ? "—" : $"{WinPercentage:0}%";

    /// <summary>Command parameters for the director's +1 / -1 chip buttons in the standings.</summary>
    public ChipAdjustmentRequest AddChipParam => new(EntrantId, +1);
    public ChipAdjustmentRequest RemoveChipParam => new(EntrantId, -1);

    /// <summary>Whether a chip can be removed from this player (they still have at least one). A
    /// chip can always be added - including to revive a just-eliminated player who buys back in.</summary>
    public bool CanRemoveChip => ChipsRemaining > 0;

    /// <summary>One-line summary for the read-only display card, e.g. "Place 2  ·  Out  ·  5-2 (71%)".</summary>
    public string SummaryLine
    {
        get
        {
            var parts = new List<string>();
            if (Place is not null) parts.Add($"Place {Place}");
            parts.Add(ChipsDisplay);
            if (MatchesPlayed > 0) parts.Add($"{RecordDisplay} ({WinPercentageDisplay})");
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
        MatchesWon = row.MatchesWon;
        MatchesPlayed = row.MatchesPlayed;
        WinPercentage = row.WinPercentage;
    }
}
