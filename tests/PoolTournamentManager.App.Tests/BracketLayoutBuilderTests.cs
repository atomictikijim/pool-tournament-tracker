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
    public void DoubleElimination_PutsFirstRoundInMiddle_LosersLeft_WinnersRight()
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

        // Boxes are added winners-first: [0..3] WB R1 (centre), [4..5] WB Final (right), [6..7] LB R1 (left).
        var winnersRound1X = layout.Boxes.Take(4).Min(b => b.X);
        var winnersFinalX = layout.Boxes.Skip(4).Take(2).Min(b => b.X);
        var losersBoxes = layout.Boxes.Skip(6).Take(2).ToList();

        Assert.All(losersBoxes, b => Assert.True(b.X < winnersRound1X, "Losers bracket should sit left of the central first round."));
        Assert.True(winnersFinalX > winnersRound1X, "Winners bracket should progress rightward from the middle.");
        Assert.Equal(losersBoxes.Min(b => b.X), layout.Boxes.Min(b => b.X), precision: 3); // losers side is leftmost
    }

    [Fact]
    public void DoubleElimination_GrandFinalIsRightmost_WithLosersChampionFeedbackLane()
    {
        var rounds = new List<RoundGroupViewModel>
        {
            Round(1, "WB Round 1", 4, BracketSide.Winners),
            Round(2, "WB Semifinals", 2, BracketSide.Winners),
            Round(3, "WB Final", 1, BracketSide.Winners),
            Round(1, "LB Round 1", 2, BracketSide.Losers),
            Round(2, "LB Round 2", 2, BracketSide.Losers),
            Round(3, "LB Final", 1, BracketSide.Losers),
            Round(1, "Grand Final", 1, BracketSide.GrandFinal),
        };

        var layout = BracketLayoutBuilder.Build(rounds);

        var grandFinal = layout.Boxes.Last(); // GF is placed after all winners/losers boxes
        Assert.Equal(layout.Boxes.Max(b => b.X), grandFinal.X, precision: 3); // rightmost column

        // The losers champion is routed under the whole bracket back into the grand final:
        // a horizontal connector segment below every match box.
        var maxBottom = layout.Boxes.Max(b => b.Bottom);
        Assert.Contains(layout.Connectors, c => c.Y1 == c.Y2 && c.Y1 > maxBottom);
    }

    [Fact]
    public void ModifiedSingleElimination_FoldsFinalStageIntoWinnersProgression_LosersToTheLeft()
    {
        var rounds = new List<RoundGroupViewModel>
        {
            Round(1, "Winners Round 1", 4, BracketSide.Winners),
            Round(2, "Winners Round 2", 2, BracketSide.Winners),
            Round(1, "Losers Round 1", 2, BracketSide.Losers),
            Round(2, "Semifinals", 2, BracketSide.Final),
            Round(3, "Final", 1, BracketSide.Final),
        };

        var layout = BracketLayoutBuilder.Build(rounds);

        // No separate "Final Rounds" section - only Winners/Losers bands.
        Assert.DoesNotContain(layout.SectionLabels, l => l.Text == "Final Rounds");
        Assert.Contains(layout.SectionLabels, l => l.Text == "Winners Bracket");
        Assert.Contains(layout.SectionLabels, l => l.Text == "Losers Bracket");

        // Box order: [0..3] WR1 (centre), [4..5] WR2, [6..7] Semifinals, [8] Final, [9..10] Losers R1.
        var winnersRound1X = layout.Boxes.Take(4).Min(b => b.X);
        var winnersMaxX = layout.Boxes.Take(6).Max(b => b.X);
        var finalBoxes = layout.Boxes.Skip(6).Take(3).ToList(); // 2 semifinal + 1 final

        // Final stage continues the winners progression as trailing columns to the RIGHT.
        Assert.Equal(3, finalBoxes.Count);
        Assert.All(finalBoxes, b => Assert.True(b.X > winnersMaxX, "Final stage should trail to the right of the winners columns."));
        Assert.Equal(2, finalBoxes.Select(b => b.X).Distinct().Count());

        // Losers bracket sits to the LEFT of the central first winners round.
        var losersBoxes = layout.Boxes.Skip(9).Take(2).ToList();
        Assert.Equal(2, losersBoxes.Count);
        Assert.All(losersBoxes, b => Assert.True(b.X < winnersRound1X, "Losers bracket should sit left of the central first round."));
    }
}
