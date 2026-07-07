using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.Core.Tests;

public class DoubleEliminationBracketTests
{
    private readonly BracketGenerationService _service = new();

    private static Tournament BuildTournament(int entrantCount)
    {
        var tournament = new Tournament { Name = "Test", Format = TournamentFormat.DoubleElimination };
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

    private void Play(Tournament tournament, int seedA, int seedB, int winnerSeed)
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
    public void GenerateDoubleElimination_ThrowsWithFewerThanTwoEntrants()
    {
        var tournament = BuildTournament(1);
        Assert.Throws<InvalidOperationException>(() => _service.GenerateDoubleElimination(tournament));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    public void GenerateDoubleElimination_ThrowsForNonPowerOfTwoEntrantCount(int entrantCount)
    {
        var tournament = BuildTournament(entrantCount);
        Assert.Throws<InvalidOperationException>(() => _service.GenerateDoubleElimination(tournament));
    }

    [Fact]
    public void GenerateDoubleElimination_Size4_CreatesExpectedNodeShape()
    {
        var tournament = BuildTournament(4);

        var bracket = _service.GenerateDoubleElimination(tournament);

        Assert.Equal(BracketKind.DoubleElimination, bracket.Kind);
        Assert.Equal(3, bracket.Nodes.Count(n => n.Side == BracketSide.Winners)); // round1 (2) + WB final (1)
        Assert.Equal(2, bracket.Nodes.Count(n => n.Side == BracketSide.Losers));
        Assert.Equal(1, bracket.Nodes.Count(n => n.Side == BracketSide.GrandFinal));
        Assert.Equal(2, tournament.Matches.Count); // only the 2 round-1 WB matches exist so far
    }

    [Fact]
    public void PlayThroughSize4Bracket_WinnersSideSweep_CompletesWithoutReset()
    {
        var tournament = BuildTournament(4);
        _service.GenerateDoubleElimination(tournament);

        // WB round 1: seed1 vs seed4, seed2 vs seed3.
        Play(tournament, 1, 4, winnerSeed: 1);
        Play(tournament, 2, 3, winnerSeed: 2);

        // LB round 1: loser(1v4)=seed4 vs loser(2v3)=seed3.
        Play(tournament, 4, 3, winnerSeed: 4);

        // WB final: seed1 vs seed2.
        Play(tournament, 1, 2, winnerSeed: 1);

        // LB final: winner(LB round1)=seed4 vs loser(WB final)=seed2.
        Play(tournament, 4, 2, winnerSeed: 4);

        Assert.Equal(TournamentStatus.InProgress, tournament.Status);

        // Grand Final: WB champion (seed1) vs LB champion (seed4). WB side wins outright.
        Play(tournament, 1, 4, winnerSeed: 1);

        Assert.Equal(TournamentStatus.Completed, tournament.Status);
        Assert.Equal(6, tournament.Matches.Count); // 2N-2 for N=4, no reset needed
    }

    [Fact]
    public void PlayThroughSize4Bracket_LosersSideComeback_TriggersBracketReset()
    {
        var tournament = BuildTournament(4);
        _service.GenerateDoubleElimination(tournament);

        Play(tournament, 1, 4, winnerSeed: 1);
        Play(tournament, 2, 3, winnerSeed: 2);
        Play(tournament, 4, 3, winnerSeed: 4);
        Play(tournament, 1, 2, winnerSeed: 1);
        Play(tournament, 4, 2, winnerSeed: 4);

        // Grand Final: LB champion (seed4) upsets the WB champion (seed1) -> forces a reset.
        Play(tournament, 1, 4, winnerSeed: 4);
        Assert.Equal(TournamentStatus.InProgress, tournament.Status);
        Assert.Equal(7, tournament.Matches.Count); // reset match materialized

        var resetNode = tournament.Bracket!.Nodes.Single(n => n.IsGrandFinalReset);
        Assert.NotNull(resetNode.Match);
        Assert.Equal(MatchStatus.Scheduled, resetNode.Match!.Status);

        // Bracket reset decider: either player can win it.
        Play(tournament, 1, 4, winnerSeed: 4);
        Assert.Equal(TournamentStatus.Completed, tournament.Status);

        var champion = BySeed(tournament, 4);
        Assert.Equal(champion.Id, resetNode.Match!.WinnerEntrantId);
    }

    [Fact]
    public void PlayThroughSize8Bracket_ExercisesLosersConsolidationRounds_DeclaresChampion()
    {
        var tournament = BuildTournament(8);
        _service.GenerateDoubleElimination(tournament);

        // WB round 1 (seeding chart pairs: 1v8, 4v5, 2v7, 3v6). All higher seeds advance.
        Play(tournament, 1, 8, winnerSeed: 1);
        Play(tournament, 4, 5, winnerSeed: 4);
        Play(tournament, 2, 7, winnerSeed: 2);
        Play(tournament, 3, 6, winnerSeed: 3);

        // LB round 1: loser(1v8)=8 vs loser(4v5)=5; loser(2v7)=7 vs loser(3v6)=6.
        Play(tournament, 8, 5, winnerSeed: 5);
        Play(tournament, 7, 6, winnerSeed: 6);

        // WB round 2 (semis): 1v4, 2v3.
        Play(tournament, 1, 4, winnerSeed: 1);
        Play(tournament, 2, 3, winnerSeed: 2);

        // LB round 2: survivor(5) vs loser(WB semi 1v4)=4; survivor(6) vs loser(2v3)=3.
        Play(tournament, 5, 4, winnerSeed: 5);
        Play(tournament, 6, 3, winnerSeed: 6);

        // LB round 3 (consolidation): survivor(5) vs survivor(6).
        Play(tournament, 5, 6, winnerSeed: 5);

        // WB final: 1 vs 2.
        Play(tournament, 1, 2, winnerSeed: 1);

        // LB final (round 4): survivor(5) vs loser(WB final)=2.
        Play(tournament, 5, 2, winnerSeed: 5);

        Assert.Equal(TournamentStatus.InProgress, tournament.Status);

        // Grand Final: WB champion (1) vs LB champion (5).
        Play(tournament, 1, 5, winnerSeed: 1);

        Assert.Equal(TournamentStatus.Completed, tournament.Status);
        Assert.Equal(14, tournament.Matches.Count); // 2N-2 for N=8, no reset
    }
}
