using PoolTournamentManager.App.Services;
using PoolTournamentManager.App.ViewModels;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.App.Tests;

public class BracketLayoutBuilderTests
{
    private static MatchRowViewModel Row() =>
        new(new Match { Player1EntrantId = Guid.NewGuid(), Player2EntrantId = Guid.NewGuid() });

    private static RoundGroupViewModel Round(int number, string title, int matchCount, BracketSide? side) =>
        new(number, title, Enumerable.Range(0, matchCount).Select(_ => Row()).ToList(), side);

    [Fact]
    public void EmptyInput_ProducesEmptyLayout()
    {
        var layout = BracketLayoutBuilder.Build(new List<RoundGroupViewModel>());

        Assert.False(layout.HasContent);
        Assert.Empty(layout.Boxes);
    }

    [Fact]
    public void SingleElimination_LaysOutOneColumnPerRound()
    {
        var rounds = new List<RoundGroupViewModel>
        {
            Round(1, "Round 1", 4, BracketSide.Winners),
            Round(2, "Semifinals", 2, BracketSide.Winners),
            Round(3, "Final", 1, BracketSide.Winners),
        };

        var layout = BracketLayoutBuilder.Build(rounds);

        Assert.Equal(7, layout.Boxes.Count);

        // Three columns, evenly spaced left-to-right.
        var columns = layout.Boxes.Select(b => b.X).Distinct().OrderBy(x => x).ToList();
        Assert.Equal(3, columns.Count);
        Assert.Equal(columns[1] - columns[0], columns[2] - columns[1], precision: 3);

        // Later columns hold progressively fewer boxes.
        Assert.Equal(4, layout.Boxes.Count(b => b.X == columns[0]));
        Assert.Equal(2, layout.Boxes.Count(b => b.X == columns[1]));
        Assert.Equal(1, layout.Boxes.Count(b => b.X == columns[2]));
    }

    [Fact]
    public void SingleElimination_CentersEachMatchBetweenItsTwoFeeders()
    {
        var rounds = new List<RoundGroupViewModel>
        {
            Round(1, "Round 1", 4, BracketSide.Winners),
            Round(2, "Semifinals", 2, BracketSide.Winners),
            Round(3, "Final", 1, BracketSide.Winners),
        };

        var layout = BracketLayoutBuilder.Build(rounds);
        var columns = layout.Boxes.Select(b => b.X).Distinct().OrderBy(x => x).ToList();

        var semis = layout.Boxes.Where(b => b.X == columns[1]).OrderBy(b => b.Y).ToList();
        var final = layout.Boxes.Single(b => b.X == columns[2]);

        Assert.Equal((semis[0].CenterY + semis[1].CenterY) / 2, final.CenterY, precision: 3);

        // Each elbow connector is three segments; 2 semifinal + 1 final feeder-pairs = 3 pairs.
        Assert.Equal(3 * 2 * 3, layout.Connectors.Count);
    }

    [Fact]
    public void DoubleElimination_StacksWinnersAboveLosersWithSectionLabels()
    {
        var rounds = new List<RoundGroupViewModel>
        {
            Round(1, "WB Round 1", 4, BracketSide.Winners),
            Round(2, "WB Final", 2, BracketSide.Winners),
            Round(1, "LB Round 1", 2, BracketSide.Losers),
        };

        var layout = BracketLayoutBuilder.Build(rounds);

        Assert.Contains(layout.SectionLabels, l => l.Text == "Winners Bracket");
        Assert.Contains(layout.SectionLabels, l => l.Text == "Losers Bracket");

        var winnersBottom = layout.Boxes.Take(6).Max(b => b.Y + b.Height);
        var losersTop = layout.Boxes.Skip(6).Min(b => b.Y);
        Assert.True(losersTop > winnersBottom, "Losers band should sit entirely below the winners band.");
    }
}
