using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.Core.Tests;

public class BracketGenerationServiceTests
{
    private readonly BracketGenerationService _service = new();

    private static Tournament BuildTournament(int entrantCount)
    {
        var tournament = new Tournament { Name = "Test", Format = TournamentFormat.SingleElimination };
        for (var i = 1; i <= entrantCount; i++)
        {
            tournament.Entrants.Add(new TournamentEntrant
            {
                TournamentId = tournament.Id,
                PlayerId = Guid.NewGuid(),
                SeedNumber = i
            });
        }
        return tournament;
    }

    private static TournamentEntrant BySeed(Tournament tournament, int seed) =>
        tournament.Entrants.First(e => e.SeedNumber == seed);

    [Theory]
    [InlineData(2, 1)]
    [InlineData(4, 3)]
    [InlineData(8, 7)]
    [InlineData(16, 15)]
    public void GenerateSingleElimination_CreatesExpectedNodeCount_ForPowerOfTwo(int entrantCount, int expectedNodes)
    {
        var tournament = BuildTournament(entrantCount);

        var bracket = _service.GenerateSingleElimination(tournament);

        Assert.Equal(expectedNodes, bracket.Nodes.Count);
        Assert.Equal(entrantCount / 2, bracket.Nodes.Count(n => n.RoundNumber == 1));
        Assert.All(bracket.Nodes.Where(n => n.RoundNumber == 1), n => Assert.False(n.Match!.IsBye));
    }

    [Fact]
    public void GenerateSingleElimination_Size8_PairsSeedsPerStandardSeedingChart()
    {
        var tournament = BuildTournament(8);

        _service.GenerateSingleElimination(tournament);

        AssertMatchup(tournament, 1, 8);
        AssertMatchup(tournament, 4, 5);
        AssertMatchup(tournament, 2, 7);
        AssertMatchup(tournament, 3, 6);
    }

    private static void AssertMatchup(Tournament tournament, int seedA, int seedB)
    {
        var a = BySeed(tournament, seedA);
        var b = BySeed(tournament, seedB);
        var match = tournament.Matches.Single(m =>
            (m.Player1EntrantId == a.Id && m.Player2EntrantId == b.Id) ||
            (m.Player1EntrantId == b.Id && m.Player2EntrantId == a.Id));
        Assert.Equal(1, tournament.Bracket!.Nodes.Single(n => n.MatchId == match.Id).RoundNumber);
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(5, 3)]
    [InlineData(6, 2)]
    [InlineData(7, 1)]
    [InlineData(11, 5)]
    public void GenerateSingleElimination_GivesByesToTopSeeds_ForNonPowerOfTwo(int entrantCount, int expectedByeCount)
    {
        var tournament = BuildTournament(entrantCount);

        _service.GenerateSingleElimination(tournament);

        var byeMatches = tournament.Matches.Where(m => m.IsBye).ToList();
        Assert.Equal(expectedByeCount, byeMatches.Count);
        Assert.All(byeMatches, m => Assert.Equal(MatchStatus.Completed, m.Status));

        // Byes must go to the top seeds (1..expectedByeCount).
        var byeEntrantSeeds = byeMatches
            .Select(m => tournament.Entrants.First(e => e.Id == m.Player1EntrantId).SeedNumber!.Value)
            .OrderBy(s => s)
            .ToList();
        Assert.Equal(Enumerable.Range(1, expectedByeCount).ToList(), byeEntrantSeeds);
    }

    [Fact]
    public void GenerateSingleElimination_DoesNotMaterializeLaterRoundMatches_UntilBothSlotsFilled()
    {
        var tournament = BuildTournament(6); // bracketSize 8, byeCount 2

        _service.GenerateSingleElimination(tournament);

        Assert.Equal(4, tournament.Matches.Count); // 2 byes (completed) + 2 real round-1 matches (scheduled)
        var round2Nodes = tournament.Bracket!.Nodes.Where(n => n.RoundNumber == 2).ToList();
        Assert.Equal(2, round2Nodes.Count);
        Assert.All(round2Nodes, n => Assert.Null(n.MatchId));
        Assert.All(round2Nodes, n => Assert.NotNull(n.Slot1EntrantId)); // pre-filled by the bye winner
        Assert.All(round2Nodes, n => Assert.Null(n.Slot2EntrantId)); // waiting on a real round-1 result
    }

    [Fact]
    public void PlayThroughEntireBracket_OfSixEntrants_DeclaresAChampion()
    {
        var tournament = BuildTournament(6);
        _service.GenerateSingleElimination(tournament);

        // Round 1 real matches: seed4 vs seed5, seed3 vs seed6. Seeds 1 and 2 already advanced on byes.
        PlayScheduledMatchBetweenSeeds(tournament, 4, 5, winnerSeed: 4);
        PlayScheduledMatchBetweenSeeds(tournament, 3, 6, winnerSeed: 3);

        Assert.Equal(TournamentStatus.InProgress, tournament.Status);
        Assert.Equal(6, tournament.Matches.Count); // 4 from round 1 + 2 newly materialized round-2 matches

        // Round 2: seed1 vs seed4, seed2 vs seed3.
        PlayScheduledMatchBetweenSeeds(tournament, 1, 4, winnerSeed: 1);
        PlayScheduledMatchBetweenSeeds(tournament, 2, 3, winnerSeed: 2);

        Assert.Equal(TournamentStatus.InProgress, tournament.Status);
        Assert.Equal(7, tournament.Matches.Count); // + the final

        // Final: seed1 vs seed2.
        PlayScheduledMatchBetweenSeeds(tournament, 1, 2, winnerSeed: 1);

        Assert.Equal(TournamentStatus.Completed, tournament.Status);
        var champion = BySeed(tournament, 1);
        var finalMatch = tournament.Matches.Single(m => m.BracketNodeId == tournament.Bracket!.Nodes.Single(n => n.FeedsIntoWinnerNodeId == null).Id);
        Assert.Equal(champion.Id, finalMatch.WinnerEntrantId);
    }

    private void PlayScheduledMatchBetweenSeeds(Tournament tournament, int seedA, int seedB, int winnerSeed)
    {
        var a = BySeed(tournament, seedA);
        var b = BySeed(tournament, seedB);
        var match = tournament.Matches.Single(m =>
            m.Status == MatchStatus.Scheduled &&
            ((m.Player1EntrantId == a.Id && m.Player2EntrantId == b.Id) ||
             (m.Player1EntrantId == b.Id && m.Player2EntrantId == a.Id)));

        var winnerIsPlayer1 = match.Player1EntrantId == BySeed(tournament, winnerSeed).Id;
        match.Status = MatchStatus.InProgress;
        _service.RecordMatchResult(tournament, match, winnerIsPlayer1 ? 7 : 3, winnerIsPlayer1 ? 3 : 7);
    }

    [Fact]
    public void RecordMatchResult_ThrowsOnTie()
    {
        var tournament = BuildTournament(2);
        _service.GenerateSingleElimination(tournament);
        var match = tournament.Matches.Single();
        match.Status = MatchStatus.InProgress;

        Assert.Throws<InvalidOperationException>(() => _service.RecordMatchResult(tournament, match, 5, 5));
    }

    [Fact]
    public void RecordMatchResult_ThrowsIfMatchNotStarted()
    {
        var tournament = BuildTournament(2);
        _service.GenerateSingleElimination(tournament);
        var match = tournament.Matches.Single();

        Assert.Throws<InvalidOperationException>(() => _service.RecordMatchResult(tournament, match, 7, 3));
    }

    [Fact]
    public void GenerateSingleElimination_ThrowsWithFewerThanTwoEntrants()
    {
        var tournament = BuildTournament(1);

        Assert.Throws<InvalidOperationException>(() => _service.GenerateSingleElimination(tournament));
    }
}
