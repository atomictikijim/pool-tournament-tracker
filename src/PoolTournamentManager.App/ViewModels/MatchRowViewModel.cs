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

    public MatchRowViewModel(Match match)
    {
        Match = match;
    }
}
