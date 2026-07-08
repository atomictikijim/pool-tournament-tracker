using PoolTournamentManager.App.ViewModels;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.App.Tests;

public class PlayerSelectionItemTests
{
    [Fact]
    public void DisplayLabel_IsJustTheNameWhenNoRatingSystemIsSet()
    {
        var item = new PlayerSelectionItem(new Player { FirstName = "Alice", LastName = "Anderson", FargoRate = 700 });

        Assert.Equal("Alice Anderson", item.DisplayLabel);
    }

    [Fact]
    public void DisplayLabel_AppendsTheRatingForTheChosenSystem()
    {
        var item = new PlayerSelectionItem(new Player { FirstName = "Alice", LastName = "Anderson", FargoRate = 700 })
        {
            RatingSystem = RatingSystem.Fargo
        };

        Assert.Equal("Alice Anderson (Fargo: 700)", item.DisplayLabel);
    }

    [Fact]
    public void DisplayLabel_ShowsAPlaceholderDashWhenThePlayerHasNoRatingInThatSystem()
    {
        var item = new PlayerSelectionItem(new Player { FirstName = "Alice", LastName = "Anderson" })
        {
            RatingSystem = RatingSystem.ApaNineBall
        };

        Assert.Equal("Alice Anderson (APA 9-Ball: —)", item.DisplayLabel);
    }

    [Fact]
    public void DisplayLabel_UpdatesWhenRatingSystemChanges()
    {
        var item = new PlayerSelectionItem(new Player { FirstName = "Alice", LastName = "Anderson", FargoRate = 700, TapRating = "6" })
        {
            RatingSystem = RatingSystem.Fargo
        };
        Assert.Equal("Alice Anderson (Fargo: 700)", item.DisplayLabel);

        item.RatingSystem = RatingSystem.Tap;
        Assert.Equal("Alice Anderson (TAP: 6)", item.DisplayLabel);

        item.RatingSystem = null;
        Assert.Equal("Alice Anderson", item.DisplayLabel);
    }
}
