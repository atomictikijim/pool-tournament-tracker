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

    /// <summary>Plays every scheduled match with the lower seed winning, until the tournament ends
    /// (or a safety cap). Byes are already Completed, so they're skipped naturally.</summary>
    private void PlayOutLowerSeedWins(Tournament tournament)
    {
        var seedById = tournament.Entrants.ToDictionary(e => e.Id, e => e.SeedNumber!.Value);
        for (var guard = 0; guard < 1000 && tournament.Status != TournamentStatus.Completed; guard++)
        {
            var match = tournament.Matches.FirstOrDefault(m => m.Status == MatchStatus.Scheduled);
            if (match is null)
            {
                break;
            }

            var p1Wins = seedById[match.Player1EntrantId] < seedById[match.Player2EntrantId!.Value];
            match.Status = MatchStatus.InProgress;
            _service.RecordMatchResult(tournament, match, p1Wins ? 7 : 3, p1Wins ? 3 : 7);
        }
    }

    [Theory]
    [InlineData(2, 2, 0)]
    [InlineData(3, 2, 1)]
    [InlineData(4, 4, 0)]
    [InlineData(5, 4, 1)]
    [InlineData(7, 4, 3)]
    [InlineData(8, 8, 0)]
    [InlineData(11, 8, 3)]
    [InlineData(15, 8, 7)]
    [InlineData(16, 16, 0)]
    [InlineData(17, 16, 1)]
    public void DoubleEliminationBracketSize_AndWaitlist_TrimToLargestPowerOfTwoThatFits(
        int entrantCount, int expectedBracketSize, int expectedWaitlist)
    {
        Assert.Equal(expectedBracketSize, BracketGenerationService.DoubleEliminationBracketSize(entrantCount));
        Assert.Equal(expectedWaitlist, BracketGenerationService.DoubleEliminationWaitlistCount(entrantCount));
    }

    [Theory]
    [InlineData(3)]  // bracket of 2, 1 waiting
    [InlineData(5)]  // bracket of 4, 1 waiting
    [InlineData(6)]  // bracket of 4, 2 waiting
    [InlineData(7)]  // bracket of 4, 3 waiting
    [InlineData(11)] // bracket of 8, 3 waiting
    [InlineData(13)] // bracket of 8, 5 waiting
    public void GenerateDoubleElimination_NonPowerOfTwo_WaitlistsLowestSeedsAndRunsAPowerOfTwoBracket(int entrantCount)
    {
        var tournament = BuildTournament(entrantCount);
        _service.GenerateDoubleElimination(tournament);

        var bracketSize = BracketGenerationService.DoubleEliminationBracketSize(entrantCount);

        // Exactly the overflow past the largest power of two is waitlisted, and it's the LOWEST seeds
        // (seed > bracketSize) - the top seeds are guaranteed a spot.
        Assert.Equal(entrantCount - bracketSize, tournament.Entrants.Count(e => e.IsWaitlisted));
        Assert.All(tournament.Entrants.Where(e => e.IsWaitlisted), e => Assert.True(e.SeedNumber > bracketSize));
        Assert.All(tournament.Entrants.Where(e => !e.IsWaitlisted), e => Assert.True(e.SeedNumber <= bracketSize));

        // Waitlisted entrants are in no bracket node and play no match.
        var waitlistedIds = tournament.Entrants.Where(e => e.IsWaitlisted).Select(e => e.Id).ToHashSet();
        Assert.DoesNotContain(tournament.Matches, m =>
            waitlistedIds.Contains(m.Player1EntrantId) || (m.Player2EntrantId is { } p2 && waitlistedIds.Contains(p2)));

        // The bracket itself is a clean power of two: no byes anywhere.
        Assert.DoesNotContain(tournament.Matches, m => m.IsBye);

        PlayOutLowerSeedWins(tournament);
        Assert.Equal(TournamentStatus.Completed, tournament.Status);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void GenerateDoubleElimination_PowerOfTwo_HasNoByesNoWaitlistAndEliminatesEveryoneAfterExactlyTwoLosses(int entrantCount)
    {
        var tournament = BuildTournament(entrantCount);
        _service.GenerateDoubleElimination(tournament);

        // A power-of-two field is never padded and nobody waits - every entrant plays from round 1.
        Assert.DoesNotContain(tournament.Matches, m => m.IsBye);
        Assert.DoesNotContain(tournament.Entrants, e => e.IsWaitlisted);

        PlayOutLowerSeedWins(tournament);

        Assert.Equal(TournamentStatus.Completed, tournament.Status);
        Assert.DoesNotContain(tournament.Matches, m => m.Status == MatchStatus.Scheduled);

        // Count each entrant's losses across every decided match (byes have no loser). Double
        // elimination must eliminate everyone on their second loss: nobody exceeds two losses, and
        // exactly one entrant (the champion) survives with fewer than two.
        var losses = tournament.Entrants.ToDictionary(e => e.Id, _ => 0);
        foreach (var m in tournament.Matches.Where(m =>
                     m.Status == MatchStatus.Completed && m.WinnerEntrantId is not null && m.Player2EntrantId is not null))
        {
            var loserId = m.WinnerEntrantId == m.Player1EntrantId ? m.Player2EntrantId!.Value : m.Player1EntrantId;
            losses[loserId]++;
        }

        Assert.All(losses.Values, l => Assert.True(l <= 2, "no entrant may lose more than twice"));
        Assert.Equal(entrantCount - 1, losses.Values.Count(l => l == 2));

        // With the lower seed always winning, seed 1 is undefeated - it must be the survivor.
        Assert.Equal(0, losses[BySeed(tournament, 1).Id]);
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

        Assert.Equal(TournamentStatus.NotStarted, tournament.Status);

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
        Assert.Equal(TournamentStatus.NotStarted, tournament.Status);
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

        Assert.Equal(TournamentStatus.NotStarted, tournament.Status);

        // Grand Final: WB champion (1) vs LB champion (5).
        Play(tournament, 1, 5, winnerSeed: 1);

        Assert.Equal(TournamentStatus.Completed, tournament.Status);
        Assert.Equal(14, tournament.Matches.Count); // 2N-2 for N=8, no reset
    }
}
