using PoolTournamentManager.App.ViewModels;
using PoolTournamentManager.Core.Entities;

namespace PoolTournamentManager.App.Tests;

public class MatchRowViewModelTests
{
    private static TournamentEntrant Entrant(string firstName, int seed) => new()
    {
        Player = new Player { FirstName = firstName, LastName = "Test" },
        SeedNumber = seed
    };

    [Fact]
    public void Placeholder_WithBothSlotsUnresolved_ShowsTbdNotBye()
    {
        var row = new MatchRowViewModel(null, null);

        Assert.True(row.IsPlaceholder);
        Assert.Equal("TBD", row.Player1Name);
        Assert.Equal("TBD", row.Player2Name);
        Assert.Null(row.Player1Seed);
        Assert.Null(row.Player2Seed);
    }

    [Fact]
    public void Placeholder_WithOneSlotResolved_ShowsThatEntrantAndTbdForTheOther()
    {
        var alice = Entrant("Alice", 1);
        var row = new MatchRowViewModel(alice, null);

        Assert.Equal("Alice Test", row.Player1Name);
        Assert.Equal(1, row.Player1Seed);
        Assert.Equal("TBD", row.Player2Name);
        Assert.Null(row.Player2Seed);
    }

    [Fact]
    public void Placeholder_HasNoMatchState()
    {
        var row = new MatchRowViewModel(Entrant("Alice", 1), Entrant("Bob", 2));

        Assert.False(row.IsStartable);
        Assert.False(row.IsInProgress);
        Assert.False(row.IsComplete);
        Assert.False(row.HasFinishedDuration);
        Assert.Null(row.WinnerName);
        Assert.False(row.IsPlayer1Winner);
        Assert.False(row.IsPlayer2Winner);
        Assert.Equal(string.Empty, row.ElapsedDisplay);

        // Tick() must not throw for a placeholder (no Match to advance a timer on).
        row.Tick();
    }

    [Fact]
    public void RealMatch_StillReportsByeForAnEmptySecondSlot()
    {
        var row = new MatchRowViewModel(new Match { Player1EntrantId = Guid.NewGuid(), Player2EntrantId = null });

        Assert.False(row.IsPlaceholder);
        Assert.Equal("BYE", row.Player2Name);
    }
}
