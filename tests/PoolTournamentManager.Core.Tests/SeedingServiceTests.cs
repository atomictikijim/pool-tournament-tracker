using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.Core.Tests;

public class SeedingServiceTests
{
    private static TournamentEntrant Entrant(string first, string last, int? fargoRate = null) => new()
    {
        PlayerId = Guid.NewGuid(),
        Player = new Player { FirstName = first, LastName = last, FargoRate = fargoRate }
    };

    private static TournamentEntrant TeamEntrant(string name) => new()
    {
        TeamId = Guid.NewGuid(),
        Team = new Team { Name = name }
    };

    [Fact]
    public void AssignSeeds_OrdersByRatingDescending()
    {
        var low = Entrant("A", "Low", 400);
        var high = Entrant("B", "High", 700);
        var mid = Entrant("C", "Mid", 550);

        var entrants = new List<TournamentEntrant> { low, high, mid };
        SeedingService.AssignSeeds(entrants, RatingSystem.Fargo);

        Assert.Equal(1, high.SeedNumber);
        Assert.Equal(2, mid.SeedNumber);
        Assert.Equal(3, low.SeedNumber);
    }

    [Fact]
    public void AssignSeeds_SortsMissingRatingsLastByName()
    {
        var rated = Entrant("Z", "Zeta", 500);
        var unratedA = Entrant("Alice", "Aaronson");
        var unratedB = Entrant("Bob", "Bertrand");

        var entrants = new List<TournamentEntrant> { unratedB, rated, unratedA };
        SeedingService.AssignSeeds(entrants, RatingSystem.Fargo);

        Assert.Equal(1, rated.SeedNumber);
        Assert.Equal(2, unratedA.SeedNumber);
        Assert.Equal(3, unratedB.SeedNumber);
    }

    [Fact]
    public void HasRating_ReflectsWhetherTheChosenSystemHasAValue()
    {
        var withApa = Entrant("A", "Player");
        withApa.Player!.ApaNineBallSkill = 6;
        var without = Entrant("B", "Player");

        Assert.True(SeedingService.HasRating(withApa, RatingSystem.ApaNineBall));
        Assert.False(SeedingService.HasRating(without, RatingSystem.ApaNineBall));
    }

    [Fact]
    public void AssignSeeds_TeamEntrantsHaveNoRatingAndSortByName()
    {
        var zebras = TeamEntrant("Zebras");
        var aces = TeamEntrant("Aces");
        var bulls = TeamEntrant("Bulls");

        var entrants = new List<TournamentEntrant> { zebras, aces, bulls };
        SeedingService.AssignSeeds(entrants, RatingSystem.Fargo);

        Assert.Equal(1, aces.SeedNumber);
        Assert.Equal(2, bulls.SeedNumber);
        Assert.Equal(3, zebras.SeedNumber);
    }

    [Fact]
    public void HasRating_IsFalseForTeamEntrants()
    {
        var team = TeamEntrant("Aces");

        Assert.False(SeedingService.HasRating(team, RatingSystem.Fargo));
    }
}
