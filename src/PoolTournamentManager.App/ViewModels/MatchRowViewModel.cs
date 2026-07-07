using CommunityToolkit.Mvvm.ComponentModel;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.App.ViewModels;

public class MatchRowViewModel : ObservableObject
{
    public Match Match { get; }

    public string Player1Name => Match.Player1Entrant?.DisplayName ?? "TBD";
    public string Player2Name => Match.Player2EntrantId is null
        ? "BYE"
        : Match.Player2Entrant?.DisplayName ?? "TBD";
    public bool IsStartable => Match.Status == MatchStatus.Scheduled && !Match.IsBye;
    public bool IsInProgress => Match.Status == MatchStatus.InProgress;
    public bool IsComplete => Match.Status == MatchStatus.Completed;

    /// <summary>True only for a completed match that was actually timed (not an auto-completed
    /// bye, which never gets a StartedAtUtc) - gates the "Finished in ..." duration display.</summary>
    public bool HasFinishedDuration => IsComplete && Match.StartedAtUtc is not null;
    public string? WinnerName => Match.WinnerEntrantId is null
        ? null
        : Match.WinnerEntrantId == Match.Player1EntrantId
            ? Player1Name
            : Player2Name;
    public bool IsPlayer1Winner => Match.WinnerEntrantId is not null && Match.WinnerEntrantId == Match.Player1EntrantId;
    public bool IsPlayer2Winner => Match.WinnerEntrantId is not null && Match.WinnerEntrantId == Match.Player2EntrantId;

    public int? Player1Seed => Match.Player1Entrant?.SeedNumber;
    public int? Player2Seed => Match.Player2EntrantId is null ? null : Match.Player2Entrant?.SeedNumber;

    /// <summary>Per-line projections used by the read-only bracket-tree display.</summary>
    public PlayerLineViewModel Player1Line => new(Player1Name, Match.Player1Score, IsPlayer1Winner, Player1Seed);
    public PlayerLineViewModel Player2Line => new(Player2Name, Match.Player2Score, IsPlayer2Winner, Player2Seed);

    /// <summary>
    /// Elapsed time as "mm:ss" (or "h:mm:ss" past an hour) - live while in progress (measured
    /// against now), frozen at the final duration once completed, empty before the match starts.
    /// </summary>
    public string ElapsedDisplay
    {
        get
        {
            if (Match.StartedAtUtc is null)
            {
                return string.Empty;
            }

            var elapsed = (Match.FinishedAtUtc ?? DateTime.UtcNow) - Match.StartedAtUtc.Value;
            return elapsed.TotalHours >= 1
                ? elapsed.ToString(@"h\:mm\:ss")
                : elapsed.ToString(@"mm\:ss");
        }
    }

    public MatchRowViewModel(Match match)
    {
        Match = match;
    }

    /// <summary>Called once a second by TournamentStateService's shared timer to refresh the
    /// live elapsed display for whichever matches are currently in progress.</summary>
    public void Tick()
    {
        if (IsInProgress)
        {
            OnPropertyChanged(nameof(ElapsedDisplay));
        }
    }
}
