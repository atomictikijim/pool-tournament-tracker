using PoolTournamentManager.App.ViewModels;
using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.App.Services;

/// <summary>
/// Turns the flat list of round groups into a positioned bracket tree, styled after the classic
/// single/double-elimination bracket (e.g. digitalpool.com): rounds march left-to-right as
/// columns, each match is centred vertically between the two feeder matches that flow into it,
/// and elbow connector lines join them. Double elimination stacks the winners half on top of the
/// losers half in separate bands, with the grand final in its own trailing column.
///
/// Layout is derived purely from each round's (Side, RoundNumber, ordered matches) — no bracket
/// node graph is needed, because the standard pairing rules (a round with twice the matches of the
/// next feeds pairwise; an equal-count "receiving" round feeds straight across) reconstruct the
/// tree shape.
/// </summary>
public static class BracketLayoutBuilder
{
    private const double BoxWidth = 232;
    private const double BoxHeight = 62;
    private const double ColumnGap = 68;   // horizontal room between columns, where connectors run
    private const double RowGap = 22;      // vertical gap between adjacent first-round boxes
    private const double HeaderHeight = 32; // round-title band above each side
    private const double SectionGap = 56;   // vertical gap between the winners and losers bands
    private const double SectionLabelHeight = 30;
    private const double LeftPad = 10;
    private const double TopPad = 10;

    private const double ColumnStride = BoxWidth + ColumnGap;

    public static BracketLayout Build(IReadOnlyList<RoundGroupViewModel> rounds)
    {
        var layout = new BracketLayout();
        if (rounds is null || rounds.Count == 0)
        {
            return layout;
        }

        var winners = rounds.Where(r => r.Side is null or BracketSide.Winners)
            .OrderBy(r => r.RoundNumber).ToList();
        var losers = rounds.Where(r => r.Side == BracketSide.Losers)
            .OrderBy(r => r.RoundNumber).ToList();
        var grandFinals = rounds.Where(r => r.Side == BracketSide.GrandFinal)
            .OrderBy(r => r.RoundNumber).ToList();

        var isDouble = losers.Count > 0 || grandFinals.Count > 0;

        // ---- Winners band -------------------------------------------------------------------
        var winnersHeaderY = TopPad + (isDouble ? SectionLabelHeight : 0);
        var winnersBandTop = winnersHeaderY + HeaderHeight;
        var winnersBoxes = LayoutSide(layout, winners, columnBase: 0, bandTop: winnersBandTop, headerY: winnersHeaderY);

        if (isDouble)
        {
            layout.SectionLabels.Add(new BracketLabelViewModel("Winners Bracket", LeftPad, TopPad, BoxWidth * 2));
        }

        var winnersBottom = winnersBoxes.Count > 0 ? winnersBoxes.Values.Max(b => b.Y + b.Height) : winnersBandTop;
        var winnersFinal = winners.Count > 0 ? winnersBoxes.GetValueOrDefault(Key(winners[^1], 0)) : null;

        // ---- Losers band --------------------------------------------------------------------
        Dictionary<string, PositionedMatchViewModel> losersBoxes = new();
        PositionedMatchViewModel? losersFinal = null;
        if (losers.Count > 0)
        {
            var losersLabelY = winnersBottom + SectionGap;
            var losersHeaderY = losersLabelY + SectionLabelHeight;
            var losersBandTop = losersHeaderY + HeaderHeight;
            layout.SectionLabels.Add(new BracketLabelViewModel("Losers Bracket", LeftPad, losersLabelY, BoxWidth * 2));
            losersBoxes = LayoutSide(layout, losers, columnBase: 0, bandTop: losersBandTop, headerY: losersHeaderY);
            losersFinal = losersBoxes.GetValueOrDefault(Key(losers[^1], 0));
        }

        // ---- Grand final(s) -----------------------------------------------------------------
        // Placed in trailing columns, aligned to the winners-final row, with both finals feeding in.
        var gfColumnBase = winners.Count;
        PositionedMatchViewModel? previousGf = null;
        for (var i = 0; i < grandFinals.Count; i++)
        {
            var round = grandFinals[i];
            if (round.Matches.Count == 0) continue;
            var col = gfColumnBase + i;
            var x = LeftPad + col * ColumnStride;
            var centerY = winnersFinal?.CenterY ?? winnersBandTop + BoxHeight / 2;
            var y = centerY - BoxHeight / 2;

            layout.Headers.Add(new BracketLabelViewModel(round.Title, x, winnersHeaderY, BoxWidth));
            var box = new PositionedMatchViewModel(round.Matches[0], x, y, BoxWidth, BoxHeight);
            layout.Boxes.Add(box);

            if (i == 0)
            {
                if (winnersFinal is not null) AddConnector(layout, winnersFinal, box);
                if (losersFinal is not null) AddConnector(layout, losersFinal, box);
            }
            else if (previousGf is not null)
            {
                AddConnector(layout, previousGf, box);
            }

            previousGf = box;
        }

        var maxRight = layout.Boxes.Count > 0 ? layout.Boxes.Max(b => b.RightX) : LeftPad + BoxWidth;
        var maxBottom = layout.Boxes.Count > 0 ? layout.Boxes.Max(b => b.Y + b.Height) : winnersBottom;
        layout.Width = maxRight + LeftPad;
        layout.Height = maxBottom + TopPad;
        return layout;
    }

    /// <summary>
    /// Positions one bracket half (winners or losers) into columns and returns each match box
    /// keyed by (round, index). Emits the round headers and the connectors feeding each column.
    /// </summary>
    private static Dictionary<string, PositionedMatchViewModel> LayoutSide(
        BracketLayout layout,
        List<RoundGroupViewModel> sideRounds,
        int columnBase,
        double bandTop,
        double headerY)
    {
        var boxes = new Dictionary<string, PositionedMatchViewModel>();
        List<PositionedMatchViewModel>? previousColumn = null;

        for (var roundIndex = 0; roundIndex < sideRounds.Count; roundIndex++)
        {
            var round = sideRounds[roundIndex];
            var x = LeftPad + (columnBase + roundIndex) * ColumnStride;
            layout.Headers.Add(new BracketLabelViewModel(round.Title, x, headerY, BoxWidth));

            var column = new List<PositionedMatchViewModel>();
            var count = round.Matches.Count;
            var prevCount = previousColumn?.Count ?? 0;

            for (var i = 0; i < count; i++)
            {
                double centerY;
                List<PositionedMatchViewModel> feeders = new();

                if (previousColumn is null || prevCount == 0)
                {
                    // First column of this side: even vertical spacing.
                    centerY = bandTop + BoxHeight / 2 + i * (BoxHeight + RowGap);
                }
                else if (prevCount == 2 * count)
                {
                    // Standard winners-style pairing: two feeders per box.
                    feeders.Add(previousColumn[2 * i]);
                    feeders.Add(previousColumn[2 * i + 1]);
                    centerY = (feeders[0].CenterY + feeders[1].CenterY) / 2;
                }
                else if (prevCount == count)
                {
                    // "Receiving" round (losers bracket): each box lines up with its single feeder.
                    feeders.Add(previousColumn[i]);
                    centerY = feeders[0].CenterY;
                }
                else
                {
                    // Irregular fan-in: fall back to even spacing, no connectors (avoids crossings).
                    centerY = bandTop + BoxHeight / 2 + i * (BoxHeight + RowGap);
                }

                var y = centerY - BoxHeight / 2;
                var box = new PositionedMatchViewModel(round.Matches[i], x, y, BoxWidth, BoxHeight);
                boxes[Key(round, i)] = box;
                column.Add(box);
                layout.Boxes.Add(box);

                foreach (var feeder in feeders)
                {
                    AddConnector(layout, feeder, box);
                }
            }

            previousColumn = column;
        }

        return boxes;
    }

    /// <summary>Draws a three-segment elbow from the right edge of <paramref name="from"/> into the left edge of <paramref name="to"/>.</summary>
    private static void AddConnector(BracketLayout layout, PositionedMatchViewModel from, PositionedMatchViewModel to)
    {
        var midX = (from.RightX + to.X) / 2;
        var fromY = from.CenterY;
        var toY = to.CenterY;

        layout.Connectors.Add(new BracketConnectorViewModel(from.RightX, fromY, midX, fromY)); // out of feeder
        layout.Connectors.Add(new BracketConnectorViewModel(midX, fromY, midX, toY));           // vertical riser
        layout.Connectors.Add(new BracketConnectorViewModel(midX, toY, to.X, toY));             // into target
    }

    private static string Key(RoundGroupViewModel round, int index) => $"{round.Side}:{round.RoundNumber}:{index}";
}
