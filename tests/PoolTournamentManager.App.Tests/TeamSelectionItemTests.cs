using PoolTournamentManager.App.ViewModels;
using PoolTournamentManager.Core.Entities;

namespace PoolTournamentManager.App.Tests;

public class TeamSelectionItemTests
{
    [Fact]
    public void DisplayLabel_IsJustTheNameWhenNoDivisionOrLocation()
    {
        var item = new TeamSelectionItem(new Team { Name = "Sharks" });

        Assert.Equal("Sharks", item.DisplayLabel);
    }

    [Fact]
    public void DisplayLabel_AppendsDivisionOnly()
    {
        var item = new TeamSelectionItem(new Team { Name = "Sharks", Division = "A" });

        Assert.Equal("Sharks (Div A)", item.DisplayLabel);
    }

    [Fact]
    public void DisplayLabel_AppendsLocationOnly()
    {
        var item = new TeamSelectionItem(new Team { Name = "Sharks", Location = "Corner Pocket" });

        Assert.Equal("Sharks (Corner Pocket)", item.DisplayLabel);
    }

    [Fact]
    public void DisplayLabel_AppendsDivisionAndLocation()
    {
        var item = new TeamSelectionItem(new Team { Name = "Sharks", Division = "A", Location = "Corner Pocket" });

        Assert.Equal("Sharks (Div A · Corner Pocket)", item.DisplayLabel);
    }

    [Fact]
    public void DisplayLabel_IgnoresWhitespaceOnlyValues()
    {
        var item = new TeamSelectionItem(new Team { Name = "Sharks", Division = "  ", Location = "" });

        Assert.Equal("Sharks", item.DisplayLabel);
    }
}
