using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.App.ViewModels;

public class MatchRowViewModel
{
    public Match Match { get; }

    public string Player1Name => Match.Player1Entrant?.Player?.FullName ?? "TBD";
    public string Player2Name => Match.Player2EntrantId is null
        ? "BYE"
        : Match.Player2Entrant?.Player?.FullName ?? "TBD";
    public bool IsReportable => Match.Status == MatchStatus.Scheduled;
    public bool IsComplete => Match.Status == MatchStatus.Completed;
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

    public MatchRowViewModel(Match match)
    {
        Match = match;
    }
}
