using PoolTournamentManager.App.ViewModels;
using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.App.Services;

/// <summary>
/// Turns the flat list of round groups into a positioned bracket tree, styled as a centre-out
/// "bowtie": the first round is generated in the middle column, the winners bracket progresses
/// rightward to the final match, and the losers bracket progresses leftward until its champion is
/// added back into the winners side (the grand final). Single elimination has no losers side, so
/// its centre column is simply the leftmost one and the whole bracket runs left-to-right unchanged.
///
/// Layout is derived purely from each round's (Side, RoundNumber, ordered matches) — no bracket
/// node graph is needed, because the standard pairing rules (a round with twice the matches of the
/// adjacent round feeds pairwise; an equal-count "receiving" round feeds straight across)
/// reconstruct the tree shape. Box size is caller-supplied so the compact read-only display and the
/// taller editable operator view can share one algorithm.
/// </summary>
public static class BracketLayoutBuilder
{
    private const double ColumnGap = 68;   // horizontal room between columns, where connectors run
    private const double HeaderHeight = 32; // round-title band above the bracket
    private const double SectionLabelHeight = 30;
    private const double LaneGap = 26;      // vertical room for the losers-champion feedback lane
    private const double LeftPad = 10;
    private const double TopPad = 10;

    private readonly record struct Metrics(double BoxWidth, double BoxHeight, double RowGap)
    {
        public double ColumnStride => BoxWidth + ColumnGap;
    }

    public static BracketLayout Build(
        IReadOnlyList<RoundGroupViewModel> rounds,
        double boxWidth = 232,
        double boxHeight = 64,
        double rowGap = 22)
    {
        var m = new Metrics(boxWidth, boxHeight, rowGap);
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
        var finals = rounds.Where(r => r.Side == BracketSide.Final)
            .OrderBy(r => r.RoundNumber).ToList();

        var isDouble = losers.Count > 0 || grandFinals.Count > 0;

        // The first winners round sits in the middle. The losers bracket occupies the columns to
        // its left (one per losers round), so the centre column index equals the losers-round count.
        var centerColumn = losers.Count;
        var headerY = TopPad + (isDouble ? SectionLabelHeight : 0);
        var bandTop = headerY + HeaderHeight;

        // ---- Winners side: round 1 in the middle column, progressing rightward ----------------
        var winnersBoxes = LayoutSide(layout, m, winners,
            columnBase: centerColumn, columnStep: 1, bandTop, headerY, leftward: false, firstColumnCenterY: null);

        // Vertical centre of the whole bowtie = centre of the (tallest) first winners column. The
        // losers side is centred on this so the two halves stay visually balanced around round 1.
        double bandCenterY;
        if (winners.Count > 0 && winners[0].Matches.Count > 0)
        {
            var c = winners[0].Matches.Count;
            var columnHeight = c * m.BoxHeight + (c - 1) * m.RowGap;
            bandCenterY = bandTop + columnHeight / 2;
        }
        else
        {
            bandCenterY = bandTop + m.BoxHeight / 2;
        }

        // Final stage (each Modified Single Elimination pod's Final Four -> Bracket Final) is a
        // continuation of the winners progression, so it renders as trailing columns to the right,
        // top-aligned so each pod's finals stack in line with that pod's winners columns.
        var rightCursor = centerColumn + winners.Count;
        if (finals.Count > 0)
        {
            var finalsBoxes = LayoutSide(layout, m, finals,
                columnBase: rightCursor, columnStep: 1, bandTop, headerY, leftward: false, firstColumnCenterY: null);
            foreach (var (key, box) in finalsBoxes)
            {
                winnersBoxes[key] = box;
            }
            rightCursor += finals.Count;
        }

        if (isDouble)
        {
            var winnersHalfCols = winners.Count + finals.Count;
            layout.SectionLabels.Add(new BracketLabelViewModel("Winners Bracket",
                LeftPad + centerColumn * m.ColumnStride, TopPad, Math.Max(1, winnersHalfCols) * m.ColumnStride));
            if (losers.Count > 0)
            {
                layout.SectionLabels.Add(new BracketLabelViewModel("Losers Bracket",
                    LeftPad, TopPad, losers.Count * m.ColumnStride));
            }
        }

        // ---- Losers side: round 1 just left of centre, progressing leftward -------------------
        PositionedMatchViewModel? losersFinal = null;
        if (losers.Count > 0)
        {
            var losersBoxes = LayoutSide(layout, m, losers,
                columnBase: centerColumn - 1, columnStep: -1, bandTop, headerY, leftward: true, firstColumnCenterY: bandCenterY);
            losersFinal = losersBoxes.GetValueOrDefault(Key(losers[^1], 0));
        }

        // ---- Grand final(s) -------------------------------------------------------------------
        // Trailing columns on the far right, aligned to the winners-final row. The winners finalist
        // feeds straight in; the losers champion is "added back" via a lane routed under the bracket.
        var winnersFinal = winners.Count > 0 ? winnersBoxes.GetValueOrDefault(Key(winners[^1], 0)) : null;
        PositionedMatchViewModel? previousGf = null;
        PositionedMatchViewModel? firstGf = null;
        for (var i = 0; i < grandFinals.Count; i++)
        {
            var round = grandFinals[i];
            if (round.Matches.Count == 0) continue;
            var col = rightCursor + i;
            var x = LeftPad + col * m.ColumnStride;
            var centerY = winnersFinal?.CenterY ?? bandCenterY;
            var y = centerY - m.BoxHeight / 2;

            layout.Headers.Add(new BracketLabelViewModel(round.Title, x, headerY, m.BoxWidth));
            var box = new PositionedMatchViewModel(round.Matches[0], x, y, m.BoxWidth, m.BoxHeight);
            layout.Boxes.Add(box);

            if (i == 0)
            {
                firstGf = box;
                if (winnersFinal is not null) AddConnector(layout, winnersFinal, box, leftward: false);
            }
            else if (previousGf is not null)
            {
                AddConnector(layout, previousGf, box, leftward: false);
            }

            previousGf = box;
        }

        // Route the losers champion under the whole bracket up into the grand final, so the
        // "added back to the winners side" hand-off reads clearly without crossing any boxes.
        var boxesBottom = layout.Boxes.Count > 0 ? layout.Boxes.Max(b => b.Bottom) : bandTop + m.BoxHeight;
        var laneY = boxesBottom + LaneGap;
        if (firstGf is not null && losersFinal is not null)
        {
            AddFeedbackLane(layout, losersFinal, firstGf, laneY);
        }

        var maxRight = layout.Boxes.Count > 0 ? layout.Boxes.Max(b => b.RightX) : LeftPad + m.BoxWidth;
        var laneBottom = firstGf is not null && losersFinal is not null ? laneY : boxesBottom;
        layout.Width = maxRight + LeftPad;
        layout.Height = laneBottom + TopPad;
        return layout;
    }

    /// <summary>
    /// Positions one bracket half into columns and returns each match box keyed by (round, index).
    /// Columns march outward from <paramref name="columnBase"/> by <paramref name="columnStep"/>
    /// (+1 rightward for the winners side, -1 leftward for the losers side). <paramref name="leftward"/>
    /// flips the connector elbows so they exit the feeder's inner edge toward the target's outer edge.
    /// When <paramref name="firstColumnCenterY"/> is set the first column is centred on it; otherwise
    /// the first column is top-aligned at <paramref name="bandTop"/>.
    /// </summary>
    private static Dictionary<string, PositionedMatchViewModel> LayoutSide(
        BracketLayout layout,
        Metrics m,
        List<RoundGroupViewModel> sideRounds,
        int columnBase,
        int columnStep,
        double bandTop,
        double headerY,
        bool leftward,
        double? firstColumnCenterY)
    {
        var boxes = new Dictionary<string, PositionedMatchViewModel>();
        List<PositionedMatchViewModel>? previousColumn = null;

        for (var roundIndex = 0; roundIndex < sideRounds.Count; roundIndex++)
        {
            var round = sideRounds[roundIndex];
            var col = columnBase + roundIndex * columnStep;
            var x = LeftPad + col * m.ColumnStride;
            layout.Headers.Add(new BracketLabelViewModel(round.Title, x, headerY, m.BoxWidth));

            var column = new List<PositionedMatchViewModel>();
            var count = round.Matches.Count;
            var prevCount = previousColumn?.Count ?? 0;

            for (var i = 0; i < count; i++)
            {
                double centerY;
                List<PositionedMatchViewModel> feeders = new();

                if (previousColumn is null || prevCount == 0)
                {
                    // First column of this side: even vertical spacing, either centred on the band
                    // centre (losers side) or top-aligned at the band top (winners side).
                    var columnHeight = count * m.BoxHeight + (count - 1) * m.RowGap;
                    var top = firstColumnCenterY is { } cy ? cy - columnHeight / 2 : bandTop;
                    centerY = top + m.BoxHeight / 2 + i * (m.BoxHeight + m.RowGap);
                }
                else if (prevCount == 2 * count)
                {
                    // Standard pairing: two feeders per box, centred between them.
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
                    centerY = bandTop + m.BoxHeight / 2 + i * (m.BoxHeight + m.RowGap);
                }

                var y = centerY - m.BoxHeight / 2;
                var box = new PositionedMatchViewModel(round.Matches[i], x, y, m.BoxWidth, m.BoxHeight);
                boxes[Key(round, i)] = box;
                column.Add(box);
                layout.Boxes.Add(box);

                foreach (var feeder in feeders)
                {
                    AddConnector(layout, feeder, box, leftward);
                }
            }

            previousColumn = column;
        }

        return boxes;
    }

    /// <summary>
    /// Draws a three-segment elbow from <paramref name="from"/> into <paramref name="to"/>. When
    /// <paramref name="leftward"/> is false the flow runs right (out of the feeder's right edge into
    /// the target's left edge); when true it runs left (out of the feeder's left edge into the
    /// target's right edge), mirroring the elbow for the leftward-progressing losers bracket.
    /// </summary>
    private static void AddConnector(BracketLayout layout, PositionedMatchViewModel from, PositionedMatchViewModel to, bool leftward)
    {
        var fromX = leftward ? from.X : from.RightX;
        var toX = leftward ? to.RightX : to.X;
        var midX = (fromX + toX) / 2;
        var fromY = from.CenterY;
        var toY = to.CenterY;

        layout.Connectors.Add(new BracketConnectorViewModel(fromX, fromY, midX, fromY)); // out of feeder
        layout.Connectors.Add(new BracketConnectorViewModel(midX, fromY, midX, toY));     // vertical riser
        layout.Connectors.Add(new BracketConnectorViewModel(midX, toY, toX, toY));        // into target
    }

    /// <summary>
    /// Routes the losers champion from the far-left losers final down under the whole bracket and
    /// back up into the grand final, so the hand-off is visible without crossing any match boxes.
    /// </summary>
    private static void AddFeedbackLane(BracketLayout layout, PositionedMatchViewModel from, PositionedMatchViewModel to, double laneY)
    {
        layout.Connectors.Add(new BracketConnectorViewModel(from.CenterX, from.Bottom, from.CenterX, laneY)); // down from losers final
        layout.Connectors.Add(new BracketConnectorViewModel(from.CenterX, laneY, to.CenterX, laneY));          // across under the bracket
        layout.Connectors.Add(new BracketConnectorViewModel(to.CenterX, laneY, to.CenterX, to.Bottom));        // up into the grand final
    }

    private static string Key(RoundGroupViewModel round, int index) => $"{round.Side}:{round.RoundNumber}:{index}";
}
