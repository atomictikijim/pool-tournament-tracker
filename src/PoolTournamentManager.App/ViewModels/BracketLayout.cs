namespace PoolTournamentManager.App.ViewModels;

/// <summary>
/// A fully positioned bracket tree ready for a Canvas: match boxes, elbow connectors, round
/// headers and section labels, all in absolute pixel coordinates, plus the total extent so the
/// hosting ScrollViewer knows how far it scrolls. Produced by <see cref="Services.BracketLayoutBuilder"/>.
/// </summary>
public sealed class BracketLayout
{
    public List<PositionedMatchViewModel> Boxes { get; } = new();
    public List<BracketConnectorViewModel> Connectors { get; } = new();
    public List<BracketLabelViewModel> Headers { get; } = new();
    public List<BracketLabelViewModel> SectionLabels { get; } = new();

    public double Width { get; set; }
    public double Height { get; set; }

    public bool HasContent => Boxes.Count > 0;
}

/// <summary>A match box placed on the bracket canvas.</summary>
public sealed class PositionedMatchViewModel
{
    public MatchRowViewModel Match { get; }
    public double X { get; }
    public double Y { get; }
    public double Width { get; }
    public double Height { get; }

    public double CenterY => Y + Height / 2;
    public double CenterX => X + Width / 2;
    public double RightX => X + Width;
    public double Bottom => Y + Height;

    public PositionedMatchViewModel(MatchRowViewModel match, double x, double y, double width, double height)
    {
        Match = match;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}

/// <summary>A single straight segment of an elbow connector between two matches.</summary>
public sealed class BracketConnectorViewModel
{
    public double X1 { get; }
    public double Y1 { get; }
    public double X2 { get; }
    public double Y2 { get; }

    public BracketConnectorViewModel(double x1, double y1, double x2, double y2)
    {
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }
}

/// <summary>A positioned text label (a round header or a section title).</summary>
public sealed class BracketLabelViewModel
{
    public string Text { get; }
    public double X { get; }
    public double Y { get; }
    public double Width { get; }

    public BracketLabelViewModel(string text, double x, double y, double width)
    {
        Text = text;
        X = x;
        Y = y;
        Width = width;
    }
}
