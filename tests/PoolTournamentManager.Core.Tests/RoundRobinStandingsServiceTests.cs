using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.Core.Tests;

public class RoundRobinStandingsServiceTests
{
    private static TournamentEntrant MakeEntrant(Guid tournamentId, string name)
    {
        return new TournamentEntrant
        {
            TournamentId = tournamentId,
            PlayerId = Guid.NewGuid(),
            Player = new Player { FirstName = name, LastName = "Player" }
        };
    }

    private static Match CompletedMatch(Guid tournamentId, TournamentEntrant p1, TournamentEntrant p2, int score1, int score2)
    {
        return new Match
        {
            TournamentId = tournamentId,
            Player1EntrantId = p1.Id,
            Player2EntrantId = p2.Id,
            Player1Score = score1,
            Player2Score = score2,
            WinnerEntrantId = score1 > score2 ? p1.Id : p2.Id,
            Status = MatchStatus.Completed
        };
    }

    [Fact]
    public void ComputeStandings_RanksByWinsDescending()
    {
        var tournament = new Tournament { Name = "Test", Format = TournamentFormat.RoundRobin };
        var a = MakeEntrant(tournament.Id, "A");
        var b = MakeEntrant(tournament.Id, "B");
        var c = MakeEntrant(tournament.Id, "C");
        tournament.Entrants.AddRange(new[] { a, b, c });

        tournament.Matches.Add(CompletedMatch(tournament.Id, a, b, 7, 2));
        tournament.Matches.Add(CompletedMatch(tournament.Id, a, c, 7, 3));
        tournament.Matches.Add(CompletedMatch(tournament.Id, b, c, 7, 1));

        var standings = RoundRobinStandingsService.ComputeStandings(tournament);

        Assert.Equal(new[] { a, b, c }, standings.Select(s => s.Entrant));
        Assert.Equal(2, standings[0].Wins);
        Assert.Equal(1, standings[1].Wins);
        Assert.Equal(0, standings[2].Wins);
    }

    [Fact]
    public void ComputeStandings_IgnoresIncompleteMatches()
    {
        var tournament = new Tournament { Name = "Test", Format = TournamentFormat.RoundRobin };
        var a = MakeEntrant(tournament.Id, "A");
        var b = MakeEntrant(tournament.Id, "B");
        tournament.Entrants.AddRange(new[] { a, b });

        tournament.Matches.Add(new Match
        {
            TournamentId = tournament.Id,
            Player1EntrantId = a.Id,
            Player2EntrantId = b.Id,
            Status = MatchStatus.Scheduled
        });

        var standings = RoundRobinStandingsService.ComputeStandings(tournament);

        Assert.All(standings, s => Assert.Equal(0, s.Wins));
    }

    [Fact]
    public void ComputeStandings_HeadToHeadBreaksTieBeforePointDifferential()
    {
        // A beats B despite B posting a much bigger point differential elsewhere - head-to-head
        // must still place A above B, since it's checked before point differential.
        var tournament = new Tournament { Name = "Test", Format = TournamentFormat.RoundRobin };
        var a = MakeEntrant(tournament.Id, "A");
        var b = MakeEntrant(tournament.Id, "B");
        var c = MakeEntrant(tournament.Id, "C");
        tournament.Entrants.AddRange(new[] { a, b, c });

        tournament.Matches.Add(CompletedMatch(tournament.Id, a, b, 7, 6)); // A: 1-0, B: 0-1
        tournament.Matches.Add(CompletedMatch(tournament.Id, b, c, 7, 0)); // B: 1-1, C: 0-1

        var standings = RoundRobinStandingsService.ComputeStandings(tournament);

        Assert.Equal(new[] { a, b, c }, standings.Select(s => s.Entrant));
    }

    [Fact]
    public void ComputeStandings_FallsBackToPointDifferentialWhenHeadToHeadIsACycle()
    {
        // A beat B, B beat C, C beat A: a 3-way cycle where head-to-head can't separate anyone
        // (each has exactly 1 head-to-head win within the tied group), so point differential
        // must decide the order.
        var tournament = new Tournament { Name = "Test", Format = TournamentFormat.RoundRobin };
        var a = MakeEntrant(tournament.Id, "A");
        var b = MakeEntrant(tournament.Id, "B");
        var c = MakeEntrant(tournament.Id, "C");
        tournament.Entrants.AddRange(new[] { a, b, c });

        tournament.Matches.Add(CompletedMatch(tournament.Id, a, b, 7, 5)); // A diff +2
        tournament.Matches.Add(CompletedMatch(tournament.Id, b, c, 7, 2)); // B diff +5 (net +3 with loss below)
        tournament.Matches.Add(CompletedMatch(tournament.Id, c, a, 7, 6)); // C diff +1, A diff -1 (net +1)

        var standings = RoundRobinStandingsService.ComputeStandings(tournament);

        Assert.All(standings, s => Assert.Equal(1, s.Wins));
        Assert.Equal(new[] { b, a, c }, standings.Select(s => s.Entrant));
        Assert.Equal(3, standings[0].PointDifferential);
        Assert.Equal(1, standings[1].PointDifferential);
        Assert.Equal(-4, standings[2].PointDifferential);
    }
}
