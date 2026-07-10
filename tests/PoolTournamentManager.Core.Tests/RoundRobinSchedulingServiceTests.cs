using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.Core.Tests;

public class RoundRobinSchedulingServiceTests
{
    private readonly RoundRobinSchedulingService _service = new();

    private static Tournament BuildTournament(int entrantCount)
    {
        var tournament = new Tournament { Name = "Test", Format = TournamentFormat.RoundRobin };
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

    [Fact]
    public void GenerateSchedule_TooFewEntrants_Throws()
    {
        var tournament = BuildTournament(1);
        Assert.Throws<InvalidOperationException>(() => _service.GenerateSchedule(tournament));
    }

    [Theory]
    [InlineData(4, 3)]
    [InlineData(5, 5)]
    [InlineData(6, 5)]
    [InlineData(7, 7)]
    [InlineData(8, 7)]
    public void GenerateSchedule_ProducesExpectedRoundCount(int entrantCount, int expectedRounds)
    {
        var tournament = BuildTournament(entrantCount);
        _service.GenerateSchedule(tournament);

        var maxRound = tournament.Matches.Max(m => m.RoundNumber!.Value);
        Assert.Equal(expectedRounds, maxRound);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void GenerateSchedule_EveryPairPlaysExactlyOnce(int entrantCount)
    {
        var tournament = BuildTournament(entrantCount);
        _service.GenerateSchedule(tournament);

        var expectedMatchCount = entrantCount * (entrantCount - 1) / 2;
        Assert.Equal(expectedMatchCount, tournament.Matches.Count);

        var pairs = tournament.Matches
            .Select(m => (Math.Min(m.Player1EntrantId.GetHashCode(), m.Player2EntrantId!.Value.GetHashCode()),
                          Math.Max(m.Player1EntrantId.GetHashCode(), m.Player2EntrantId!.Value.GetHashCode())))
            .ToList();
        Assert.Equal(pairs.Count, pairs.Distinct().Count());

        foreach (var entrant in tournament.Entrants)
        {
            var appearances = tournament.Matches.Count(m =>
                m.Player1EntrantId == entrant.Id || m.Player2EntrantId == entrant.Id);
            Assert.Equal(entrantCount - 1, appearances);
        }
    }

    [Fact]
    public void GenerateSchedule_NoEntrantPlaysTwiceInTheSameRound()
    {
        var tournament = BuildTournament(7);
        _service.GenerateSchedule(tournament);

        foreach (var roundGroup in tournament.Matches.GroupBy(m => m.RoundNumber))
        {
            var entrantsThisRound = roundGroup.SelectMany(m => new[] { m.Player1EntrantId, m.Player2EntrantId!.Value });
            Assert.Equal(entrantsThisRound.Count(), entrantsThisRound.Distinct().Count());
        }
    }

    [Fact]
    public void GenerateSchedule_SetsTournamentNotStarted()
    {
        var tournament = BuildTournament(4);
        _service.GenerateSchedule(tournament);
        Assert.Equal(TournamentStatus.NotStarted, tournament.Status);
    }
}
