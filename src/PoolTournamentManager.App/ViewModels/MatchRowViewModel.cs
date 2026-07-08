using CommunityToolkit.Mvvm.ComponentModel;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.App.ViewModels;

public class MatchRowViewModel : ObservableObject
{
    public Match? Match { get; }

    private readonly TournamentEntrant? _placeholderPlayer1;
    private readonly TournamentEntrant? _placeholderPlayer2;
    private readonly RatingSystem? _ratingSystem;

    /// <summary>
    /// True for a bracket slot whose match hasn't materialized yet (both entrant slots aren't
    /// known, or only one is). Never represents a bye - Round 1's byes are always resolved
    /// immediately at creation, and every later-round/Grand-Final node always needs two real
    /// winners to arrive - so an empty placeholder slot always means "TBD", never "BYE".
    /// </summary>
    public bool IsPlaceholder => Match is null;

    public string Player1Name => Match is not null
        ? Match.Player1Entrant?.DisplayName ?? "TBD"
        : _placeholderPlayer1?.DisplayName ?? "TBD";
    public string Player2Name => Match is not null
        ? (Match.Player2EntrantId is null ? "BYE" : Match.Player2Entrant?.DisplayName ?? "TBD")
        : _placeholderPlayer2?.DisplayName ?? "TBD";
    public bool IsStartable => Match is not null && Match.Status == MatchStatus.Scheduled && !Match.IsBye;
    public bool IsInProgress => Match?.Status == MatchStatus.InProgress;
    public bool IsComplete => Match?.Status == MatchStatus.Completed;

    /// <summary>True only for a completed match that was actually timed (not an auto-completed
    /// bye, which never gets a StartedAtUtc) - gates the "Finished in ..." duration display.</summary>
    public bool HasFinishedDuration => IsComplete && Match?.StartedAtUtc is not null;
    public string? WinnerName => Match?.WinnerEntrantId is null
        ? null
        : Match.WinnerEntrantId == Match.Player1EntrantId
            ? Player1Name
            : Player2Name;
    public bool IsPlayer1Winner => Match?.WinnerEntrantId is not null && Match.WinnerEntrantId == Match.Player1EntrantId;
    public bool IsPlayer2Winner => Match?.WinnerEntrantId is not null && Match.WinnerEntrantId == Match.Player2EntrantId;

    public int? Player1Seed => Match is not null ? Match.Player1Entrant?.SeedNumber : _placeholderPlayer1?.SeedNumber;
    public int? Player2Seed => Match is not null
        ? (Match.Player2EntrantId is null ? null : Match.Player2Entrant?.SeedNumber)
        : _placeholderPlayer2?.SeedNumber;

    /// <summary>The entrant's rating for whichever system the tournament was seeded by (null if
    /// the tournament wasn't seeded by rating, e.g. random-draw or team formats).</summary>
    public string? Player1RatingDisplay => GetRatingDisplay(Match is not null ? Match.Player1Entrant : _placeholderPlayer1);
    public string? Player2RatingDisplay => Match is not null
        ? (Match.Player2EntrantId is null ? null : GetRatingDisplay(Match.Player2Entrant))
        : GetRatingDisplay(_placeholderPlayer2);

    private string? GetRatingDisplay(TournamentEntrant? entrant) =>
        _ratingSystem is null ? null : SeedingService.GetRatingDisplay(entrant?.Player, _ratingSystem.Value);

    /// <summary>Per-line projections used by the read-only bracket-tree display.</summary>
    public PlayerLineViewModel Player1Line => new(Player1Name, Match?.Player1Score, IsPlayer1Winner, Player1Seed, Player1RatingDisplay);
    public PlayerLineViewModel Player2Line => new(Player2Name, Match?.Player2Score, IsPlayer2Winner, Player2Seed, Player2RatingDisplay);

    /// <summary>
    /// Elapsed time as "mm:ss" (or "h:mm:ss" past an hour) - live while in progress (measured
    /// against now), frozen at the final duration once completed, empty before the match starts
    /// (including a placeholder row, which has no match to start).
    /// </summary>
    public string ElapsedDisplay
    {
        get
        {
            if (Match?.StartedAtUtc is null)
            {
                return string.Empty;
            }

            var elapsed = (Match.FinishedAtUtc ?? DateTime.UtcNow) - Match.StartedAtUtc.Value;
            return elapsed.TotalHours >= 1
                ? elapsed.ToString(@"h\:mm\:ss")
                : elapsed.ToString(@"mm\:ss");
        }
    }

    public MatchRowViewModel(Match match, RatingSystem? ratingSystem = null)
    {
        Match = match;
        _ratingSystem = ratingSystem;
    }

    /// <summary>A bracket slot whose match hasn't materialized yet - shows whichever entrant(s)
    /// have already arrived via a prior round's result, "TBD" for the rest.</summary>
    public MatchRowViewModel(TournamentEntrant? player1, TournamentEntrant? player2, RatingSystem? ratingSystem = null)
    {
        Match = null;
        _placeholderPlayer1 = player1;
        _placeholderPlayer2 = player2;
        _ratingSystem = ratingSystem;
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
