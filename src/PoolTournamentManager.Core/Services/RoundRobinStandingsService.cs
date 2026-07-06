using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.Core.Services;

public class RoundRobinStandingRow
{
    public required TournamentEntrant Entrant { get; init; }
    public int Wins { get; init; }
    public int Losses { get; init; }
    public int GamesWon { get; init; }
    public int GamesLost { get; init; }
    public int PointDifferential => GamesWon - GamesLost;
    public double GamesWonPercentage => GamesWon + GamesLost == 0 ? 0 : (double)GamesWon / (GamesWon + GamesLost);
}

/// <summary>
/// Computes round-robin standings on demand from completed Match rows - nothing about
/// win/loss/tiebreak state is ever persisted. Entrants are ranked by wins descending; entrants
/// still tied on wins are ranked by head-to-head record among just that tied group, then (still
/// tied) by point differential, then (still tied) by games-won percentage - each rule applied
/// only within the subset the previous rule couldn't separate.
/// </summary>
public static class RoundRobinStandingsService
{
    public static List<RoundRobinStandingRow> ComputeStandings(Tournament tournament)
    {
        var completedMatches = tournament.Matches
            .Where(m => m.Status == MatchStatus.Completed && m.Player2EntrantId is not null)
            .ToList();

        var rows = tournament.Entrants
            .Select(e => BuildRow(e, completedMatches))
            .ToList();

        var ordered = new List<RoundRobinStandingRow>();
        foreach (var winGroup in rows.GroupBy(r => r.Wins).OrderByDescending(g => g.Key))
        {
            ordered.AddRange(BreakTiesByHeadToHead(winGroup.ToList(), completedMatches));
        }

        return ordered;
    }

    private static RoundRobinStandingRow BuildRow(TournamentEntrant entrant, List<Match> completedMatches)
    {
        var wins = 0;
        var losses = 0;
        var gamesWon = 0;
        var gamesLost = 0;

        foreach (var match in completedMatches)
        {
            if (match.Player1EntrantId == entrant.Id)
            {
                gamesWon += match.Player1Score ?? 0;
                gamesLost += match.Player2Score ?? 0;
            }
            else if (match.Player2EntrantId == entrant.Id)
            {
                gamesWon += match.Player2Score ?? 0;
                gamesLost += match.Player1Score ?? 0;
            }
            else
            {
                continue;
            }

            if (match.WinnerEntrantId == entrant.Id)
            {
                wins++;
            }
            else
            {
                losses++;
            }
        }

        return new RoundRobinStandingRow
        {
            Entrant = entrant,
            Wins = wins,
            Losses = losses,
            GamesWon = gamesWon,
            GamesLost = gamesLost
        };
    }

    private static List<RoundRobinStandingRow> BreakTiesByHeadToHead(
        List<RoundRobinStandingRow> tiedGroup, List<Match> completedMatches)
    {
        if (tiedGroup.Count <= 1)
        {
            return tiedGroup;
        }

        var idsInGroup = tiedGroup.Select(r => r.Entrant.Id).ToHashSet();
        var headToHeadWins = tiedGroup.ToDictionary(
            r => r.Entrant.Id,
            r => completedMatches.Count(m =>
                m.WinnerEntrantId == r.Entrant.Id &&
                idsInGroup.Contains(m.Player1EntrantId) &&
                idsInGroup.Contains(m.Player2EntrantId!.Value)));

        var result = new List<RoundRobinStandingRow>();
        foreach (var subGroup in tiedGroup.GroupBy(r => headToHeadWins[r.Entrant.Id]).OrderByDescending(g => g.Key))
        {
            result.AddRange(BreakTiesByPointDifferential(subGroup.ToList()));
        }
        return result;
    }

    private static List<RoundRobinStandingRow> BreakTiesByPointDifferential(List<RoundRobinStandingRow> tiedGroup)
    {
        if (tiedGroup.Count <= 1)
        {
            return tiedGroup;
        }

        var result = new List<RoundRobinStandingRow>();
        foreach (var subGroup in tiedGroup.GroupBy(r => r.PointDifferential).OrderByDescending(g => g.Key))
        {
            result.AddRange(BreakTiesByGamesWonPercentage(subGroup.ToList()));
        }
        return result;
    }

    private static List<RoundRobinStandingRow> BreakTiesByGamesWonPercentage(List<RoundRobinStandingRow> tiedGroup)
    {
        return tiedGroup
            .OrderByDescending(r => r.GamesWonPercentage)
            .ThenBy(r => r.Entrant.Player?.LastName)
            .ThenBy(r => r.Entrant.Player?.FirstName)
            .ToList();
    }
}
