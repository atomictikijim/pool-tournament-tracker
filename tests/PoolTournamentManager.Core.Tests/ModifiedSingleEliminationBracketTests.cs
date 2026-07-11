using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.Core.Tests;

public class ModifiedSingleEliminationBracketTests
{
    private readonly BracketGenerationService _service = new();

    private static Tournament BuildTournament(int entrantCount)
    {
        var tournament = new Tournament { Name = "Test", Format = TournamentFormat.ModifiedSingleElimination };
        for (var i = 1; i <= entrantCount; i++)
        {
            tournament.Entrants.Add(new TournamentEntrant { TournamentId = tournament.Id, PlayerId = Guid.NewGuid() });
        }
        return tournament;
    }

    private static BracketNode Node(Tournament tournament, BracketSide side, int roundNumber, int position) =>
        tournament.Bracket!.Nodes.Single(n => n.Side == side && n.RoundNumber == roundNumber && n.PositionInRound == position);

    /// <summary>Plays the node's currently-scheduled match, always crowning Player1 the winner -
    /// round 1 is a random draw by design, so these tests assert on bracket wiring/shape (who
    /// advances/is eliminated structurally), not on which specific entrant wins.</summary>
    private IReadOnlyList<Match> PlayNode(Tournament tournament, BracketNode node)
    {
        var match = node.Match!;
        match.Status = MatchStatus.InProgress;
        return _service.RecordMatchResult(tournament, match, 7, 3);
    }

    [Theory]
    [InlineData(1)]  // below the 6-entrant floor
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(9)]  // a second bracket couldn't reach 6
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(17)] // a third bracket couldn't reach 6 (9-11 and 17 are the only invalid counts >= 6)
    public void GenerateModifiedSingleElimination_ThrowsForCountsThatCannotSplitIntoBracketsOfSixToEight(int entrantCount)
    {
        Assert.False(BracketGenerationService.IsValidModifiedSingleEliminationCount(entrantCount));
        var tournament = BuildTournament(entrantCount);
        Assert.Throws<InvalidOperationException>(() => _service.GenerateModifiedSingleElimination(tournament));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    [InlineData(18)]
    [InlineData(24)]
    public void IsValidModifiedSingleEliminationCount_AcceptsCountsThatSplitIntoBracketsOfSixToEight(int entrantCount)
    {
        Assert.True(BracketGenerationService.IsValidModifiedSingleEliminationCount(entrantCount));
    }

    [Theory]
    [InlineData(6, new[] { 6 })]
    [InlineData(7, new[] { 7 })]
    [InlineData(8, new[] { 8 })]
    [InlineData(12, new[] { 6, 6 })]
    [InlineData(15, new[] { 8, 7 })]
    [InlineData(16, new[] { 8, 8 })]
    [InlineData(18, new[] { 6, 6, 6 })]
    [InlineData(20, new[] { 7, 7, 6 })]
    [InlineData(24, new[] { 8, 8, 8 })]
    public void ModifiedSingleEliminationPodSizes_SplitsEvenlyAcrossFewestPods(int entrantCount, int[] expected)
    {
        Assert.Equal(expected, BracketGenerationService.ModifiedSingleEliminationPodSizes(entrantCount));
        Assert.All(expected, size => Assert.InRange(size, 6, 8));
    }

    /// <summary>Plays every scheduled match (crowning Player1) until the tournament ends. Byes are
    /// already Completed, so they're skipped naturally.</summary>
    private void PlayOut(Tournament tournament)
    {
        for (var guard = 0; guard < 2000 && tournament.Status != TournamentStatus.Completed; guard++)
        {
            var match = tournament.Matches.FirstOrDefault(m => m.Status == MatchStatus.Scheduled);
            if (match is null)
            {
                break;
            }
            match.Status = MatchStatus.InProgress;
            _service.RecordMatchResult(tournament, match, 7, 3);
        }
    }

    [Theory]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(18)]
    [InlineData(20)]
    [InlineData(22)]
    [InlineData(24)]
    [InlineData(30)]
    public void GenerateModifiedSingleElimination_MultiBracket_PlaysToCompletionWithOneWinnerPerBracket(int entrantCount)
    {
        var tournament = BuildTournament(entrantCount);
        _service.GenerateModifiedSingleElimination(tournament);

        var expectedPods = BracketGenerationService.ModifiedSingleEliminationPodSizes(entrantCount).Length;

        // Every pod contributes 2 Final Four nodes (Final round 1) and exactly one terminal
        // Bracket Final (Final round 2 with no onward winner feed).
        Assert.Equal(expectedPods * 2, tournament.Bracket!.Nodes.Count(n => n.Side == BracketSide.Final && n.RoundNumber == 1));
        var championNodes = tournament.Bracket!.Nodes
            .Where(n => n.Side == BracketSide.Final && n.FeedsIntoWinnerNodeId is null)
            .ToList();
        Assert.Equal(expectedPods, championNodes.Count);

        PlayOut(tournament);

        Assert.Equal(TournamentStatus.Completed, tournament.Status);
        Assert.DoesNotContain(tournament.Matches, m => m.Status == MatchStatus.Scheduled);

        // One winner per independent bracket - no cross-pod stage merges them into a single champion.
        Assert.Equal(expectedPods, PrizePayoutService.ComputeQualifiers(tournament).Count);
    }

    [Fact]
    public void GenerateModifiedSingleElimination_Size8_CreatesExpectedNodeShapeAndRandomDraw()
    {
        var tournament = BuildTournament(8);
        var bracket = _service.GenerateModifiedSingleElimination(tournament);

        Assert.Equal(BracketKind.ModifiedSingleElimination, bracket.Kind);
        Assert.Equal(6, bracket.Nodes.Count(n => n.Side == BracketSide.Winners)); // R1 (4) + WB R2 (2)
        Assert.Equal(4, bracket.Nodes.Count(n => n.Side == BracketSide.Losers));  // LB R1 (2) + LB R2 (2)
        Assert.Equal(3, bracket.Nodes.Count(n => n.Side == BracketSide.Final));   // Final Four (2) + Bracket Final (1)
        Assert.Equal(4, tournament.Matches.Count); // only round 1 materializes up front

        // Round 1 is a random draw, not a rating seed, but SeedNumber still records the draw order.
        Assert.Equal(Enumerable.Range(1, 8), tournament.Entrants.Select(e => e.SeedNumber!.Value).OrderBy(n => n));
    }

    [Fact]
    public void PlayThroughSize8Pod_MatchesOfficialThirteenMatchDiagram()
    {
        var tournament = BuildTournament(8);
        _service.GenerateModifiedSingleElimination(tournament);

        // Round 1: 4 matches. Their losers drop to Losers Round 1, winners to Winners Round 2.
        foreach (var node in Enumerable.Range(0, 4).Select(i => Node(tournament, BracketSide.Winners, 1, i)))
        {
            PlayNode(tournament, node);
        }
        Assert.Equal(8, tournament.Matches.Count); // +2 Losers Round 1, +2 Winners Round 2

        // Losers Round 1: a loss here eliminates outright - no further match for these players.
        foreach (var node in Enumerable.Range(0, 2).Select(i => Node(tournament, BracketSide.Losers, 1, i)))
        {
            PlayNode(tournament, node);
        }
        Assert.Equal(8, tournament.Matches.Count); // Losers Round 2 still needs Winners Round 2's losers too

        // Winners Round 2: winners are still undefeated; losers drop to Losers Round 2.
        foreach (var node in Enumerable.Range(0, 2).Select(i => Node(tournament, BracketSide.Winners, 2, i)))
        {
            PlayNode(tournament, node);
        }
        Assert.Equal(10, tournament.Matches.Count); // Losers Round 2 (2) now materializes

        // Losers Round 2 ("receiving"): a loss here eliminates outright.
        foreach (var node in Enumerable.Range(0, 2).Select(i => Node(tournament, BracketSide.Losers, 2, i)))
        {
            PlayNode(tournament, node);
        }
        Assert.Equal(12, tournament.Matches.Count); // Final Four (2) now materializes

        // Final Four: from here on it's single elimination, no more drops.
        foreach (var node in Enumerable.Range(0, 2).Select(i => Node(tournament, BracketSide.Final, 1, i)))
        {
            PlayNode(tournament, node);
        }
        Assert.Equal(13, tournament.Matches.Count); // the Final now materializes
        Assert.Equal(TournamentStatus.NotStarted, tournament.Status);

        // Final: sudden death, no bracket-reset decider.
        PlayNode(tournament, Node(tournament, BracketSide.Final, 2, 0));

        Assert.Equal(TournamentStatus.Completed, tournament.Status);
        Assert.Equal(13, tournament.Matches.Count); // matches the official APA 13-match diagram
        Assert.DoesNotContain(tournament.Bracket!.Nodes, n => n.IsGrandFinalReset);
    }

    [Fact]
    public void PlayThrough16Entrants_TwoIndependentBrackets_EachCrownsItsOwnWinner()
    {
        var tournament = BuildTournament(16);
        _service.GenerateModifiedSingleElimination(tournament);

        Assert.Equal(8, tournament.Matches.Count); // both pods' Round 1 (4 each) materialize up front

        // Each pod is a fully independent bracket: no cross-pod nodes exist. Its Final side is just
        // its own Final Four (round 1) and its single Bracket Final (round 2, position = podIndex).
        Assert.Equal(4, tournament.Bracket!.Nodes.Count(n => n.Side == BracketSide.Final && n.RoundNumber == 1)); // 2 per pod
        Assert.Equal(2, tournament.Bracket!.Nodes.Count(n => n.Side == BracketSide.Final && n.RoundNumber == 2)); // 1 per pod
        Assert.Empty(tournament.Bracket!.Nodes.Where(n => n.Side == BracketSide.Final && n.RoundNumber > 2)); // no cross-pod stage

        // Plays a whole pod, right through its Bracket Final, crowning that bracket's one winner.
        void PlayPodFully(int podIndex)
        {
            foreach (var node in Enumerable.Range(podIndex * 4, 4).Select(i => Node(tournament, BracketSide.Winners, 1, i)))
            {
                PlayNode(tournament, node);
            }
            foreach (var node in Enumerable.Range(podIndex * 2, 2).Select(i => Node(tournament, BracketSide.Losers, 1, i)))
            {
                PlayNode(tournament, node);
            }
            foreach (var node in Enumerable.Range(podIndex * 2, 2).Select(i => Node(tournament, BracketSide.Winners, 2, i)))
            {
                PlayNode(tournament, node);
            }
            foreach (var node in Enumerable.Range(podIndex * 2, 2).Select(i => Node(tournament, BracketSide.Losers, 2, i)))
            {
                PlayNode(tournament, node);
            }
            foreach (var node in Enumerable.Range(podIndex * 2, 2).Select(i => Node(tournament, BracketSide.Final, 1, i)))
            {
                PlayNode(tournament, node);
            }
            PlayNode(tournament, Node(tournament, BracketSide.Final, 2, podIndex)); // Bracket Final
        }

        PlayPodFully(0);
        // Pod 0 is fully decided (its own 13-match diagram) but pod 1 hasn't been touched, so the
        // tournament is NOT complete - a bracket finishing early doesn't end it. The count is 17:
        // pod 0's 13 matches plus pod 1's 4 round-1 matches, which materialized up front.
        Assert.Equal(17, tournament.Matches.Count);
        Assert.NotEqual(TournamentStatus.Completed, tournament.Status);

        PlayPodFully(1);

        // Both brackets done: 13 matches each, no cross-pod matches at all.
        Assert.Equal(26, tournament.Matches.Count);
        Assert.Equal(TournamentStatus.Completed, tournament.Status);

        // Two co-equal winners, one per bracket, in bracket order.
        var qualifiers = PrizePayoutService.ComputeQualifiers(tournament);
        Assert.Equal(2, qualifiers.Count);
        Assert.Equal(2, qualifiers.Select(q => q.Id).Distinct().Count());
    }
}
