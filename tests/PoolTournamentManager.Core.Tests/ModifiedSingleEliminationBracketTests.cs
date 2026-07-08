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
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    public void GenerateModifiedSingleElimination_ThrowsForFewerThanOneFullPod(int entrantCount)
    {
        var tournament = BuildTournament(entrantCount);
        Assert.Throws<InvalidOperationException>(() => _service.GenerateModifiedSingleElimination(tournament));
    }

    [Theory]
    [InlineData(8, new[] { 8 })]
    [InlineData(9, new[] { 5, 4 })]
    [InlineData(12, new[] { 6, 6 })]
    [InlineData(15, new[] { 8, 7 })]
    [InlineData(16, new[] { 8, 8 })]
    [InlineData(17, new[] { 6, 6, 5 })]
    [InlineData(20, new[] { 7, 7, 6 })]
    [InlineData(24, new[] { 8, 8, 8 })]
    public void ModifiedSingleEliminationPodSizes_SplitsEvenlyAcrossFewestPods(int entrantCount, int[] expected)
    {
        Assert.Equal(expected, BracketGenerationService.ModifiedSingleEliminationPodSizes(entrantCount));
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
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(15)]
    [InlineData(17)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(30)]
    public void GenerateModifiedSingleElimination_NonPowerOfTwo_PlaysToCompletion(int entrantCount)
    {
        var tournament = BuildTournament(entrantCount);
        _service.GenerateModifiedSingleElimination(tournament);

        // Every pod contributes exactly 2 reps (Final-side round-1 nodes).
        var expectedPods = BracketGenerationService.ModifiedSingleEliminationPodSizes(entrantCount).Length;
        Assert.Equal(expectedPods * 2, tournament.Bracket!.Nodes.Count(n => n.Side == BracketSide.Final && n.RoundNumber == 1));

        PlayOut(tournament);

        Assert.Equal(TournamentStatus.Completed, tournament.Status);
        Assert.DoesNotContain(tournament.Matches, m => m.Status == MatchStatus.Scheduled);
    }

    [Fact]
    public void GenerateModifiedSingleElimination_Size8_CreatesExpectedNodeShapeAndRandomDraw()
    {
        var tournament = BuildTournament(8);
        var bracket = _service.GenerateModifiedSingleElimination(tournament);

        Assert.Equal(BracketKind.ModifiedSingleElimination, bracket.Kind);
        Assert.Equal(6, bracket.Nodes.Count(n => n.Side == BracketSide.Winners)); // R1 (4) + WB R2 (2)
        Assert.Equal(4, bracket.Nodes.Count(n => n.Side == BracketSide.Losers));  // LB R1 (2) + LB R2 (2)
        Assert.Equal(3, bracket.Nodes.Count(n => n.Side == BracketSide.Final));   // Final Four (2) + Final (1)
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
        Assert.Equal(TournamentStatus.InProgress, tournament.Status);

        // Final: sudden death, no bracket-reset decider.
        PlayNode(tournament, Node(tournament, BracketSide.Final, 2, 0));

        Assert.Equal(TournamentStatus.Completed, tournament.Status);
        Assert.Equal(13, tournament.Matches.Count); // matches the official APA 13-match diagram
        Assert.DoesNotContain(tournament.Bracket!.Nodes, n => n.IsGrandFinalReset);
    }

    [Fact]
    public void PlayThrough16Entrants_TwoPodsEachContributeExactlyTwoRepsToARealSemifinal()
    {
        var tournament = BuildTournament(16);
        _service.GenerateModifiedSingleElimination(tournament);

        Assert.Equal(8, tournament.Matches.Count); // both pods' Round 1 (4 each) materialize up front

        void PlayPodThroughFinalFour(int podIndex)
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
        }

        PlayPodThroughFinalFour(0);
        // 8 initial (both pods' Round 1) + pod 0's 8 further internal matches (LB R1, WB R2,
        // LB R2, Final Four - its own Round 1 matches were already counted in the initial 8).
        Assert.Equal(16, tournament.Matches.Count);

        PlayPodThroughFinalFour(1);
        // Both pods done (12 internal matches each = 24), and now that both pods' Final-Four
        // winners are known, the 2 semifinal matches (pairing reps from DIFFERENT pods) materialize.
        Assert.Equal(26, tournament.Matches.Count);

        var semifinal = Enumerable.Range(0, 2).Select(i => Node(tournament, BracketSide.Final, 2, i)).ToList();
        Assert.Equal(2, semifinal.Count);
        Assert.All(semifinal, n => Assert.NotNull(n.Match));

        foreach (var node in semifinal)
        {
            PlayNode(tournament, node);
        }

        var final = Node(tournament, BracketSide.Final, 3, 0);
        Assert.NotNull(final.Match);
        Assert.Equal(TournamentStatus.InProgress, tournament.Status);

        PlayNode(tournament, final);

        Assert.Equal(TournamentStatus.Completed, tournament.Status);
        Assert.Equal(27, tournament.Matches.Count); // 2 pods x 12 internal + 2 semifinal + 1 final
    }
}
