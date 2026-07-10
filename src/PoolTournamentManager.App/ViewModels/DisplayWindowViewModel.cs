using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoolTournamentManager.App.Services;
using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.App.ViewModels;

/// <summary>
/// Read-only by construction with respect to tournament data: this ViewModel exposes no ICommand
/// mutators that change tournament state, only projections over the shared
/// TournamentStateService. The bracket zoom commands are the one exception - they only affect
/// this window's own local display scale, never anything persisted or shared.
/// </summary>
public partial class DisplayWindowViewModel : ObservableObject
{
    private const double MinBracketZoom = 0.15;
    private const double MaxBracketZoom = 2.0;
    private const double BracketZoomStep = 0.1;

    public TournamentStateService State { get; }

    public DisplayWindowViewModel(TournamentStateService state)
    {
        State = state;
        State.PropertyChanged += OnStateChanged;
        RebuildBracket();
    }

    /// <summary>Scale factor applied to the bracket tree via a LayoutTransform. 1.0 = actual size.</summary>
    [ObservableProperty]
    private double _bracketZoom = 1.0;

    /// <summary>"100%"-style text for the zoom control's readout.</summary>
    public string BracketZoomDisplay => BracketZoom.ToString("P0");

    partial void OnBracketZoomChanged(double value) => OnPropertyChanged(nameof(BracketZoomDisplay));

    [RelayCommand]
    private void ZoomBracketIn() => BracketZoom = Math.Min(MaxBracketZoom, Math.Round(BracketZoom + BracketZoomStep, 2));

    [RelayCommand]
    private void ZoomBracketOut() => BracketZoom = Math.Max(MinBracketZoom, Math.Round(BracketZoom - BracketZoomStep, 2));

    [RelayCommand]
    private void ResetBracketZoom() => BracketZoom = 1.0;

    /// <summary>Sets the zoom to whatever scale fits the bracket's full extent into the given
    /// viewport (clamped to the same range the +/- buttons respect). The viewport size is only
    /// known to the view (a ScrollViewer's measured size), so the "Fit" button's code-behind
    /// click handler computes it and calls this rather than a bare property setter.</summary>
    public void FitBracketToViewport(double viewportWidth, double viewportHeight)
    {
        if (Bracket.Width <= 0 || Bracket.Height <= 0 || viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }

        var scale = Math.Min(viewportWidth / Bracket.Width, viewportHeight / Bracket.Height);
        BracketZoom = Math.Clamp(Math.Round(scale, 2), MinBracketZoom, MaxBracketZoom);
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TournamentStateService.ActiveTournament)
            or nameof(TournamentStateService.Tables)
            or nameof(TournamentStateService.Rounds))
        {
            OnPropertyChanged(nameof(TableAssignments));
            RebuildBracket();
        }
    }

    /// <summary>The positioned bracket tree for elimination formats (empty otherwise).</summary>
    public BracketLayout Bracket { get; private set; } = new();

    /// <summary>True when the active tournament is a single/double-elimination bracket.</summary>
    public bool IsEliminationBracket { get; private set; }

    /// <summary>True for round-robin, which falls back to the simple round-column list.</summary>
    public bool ShowFlatRounds { get; private set; }

    /// <summary>Which faded ball watermark (see the Grid behind the bracket in DisplayWindow.xaml)
    /// matches the active tournament's game - only one of these three is ever true at a time.</summary>
    public bool IsEightBallGame { get; private set; }
    public bool IsNineBallGame { get; private set; }
    public bool IsTenBallGame { get; private set; }

    private void RebuildBracket()
    {
        var format = State.ActiveTournament?.Format;
        IsEliminationBracket = format is TournamentFormat.SingleElimination or TournamentFormat.DoubleElimination
            or TournamentFormat.ModifiedSingleElimination;
        ShowFlatRounds = format is TournamentFormat.RoundRobin;
        Bracket = IsEliminationBracket ? BracketLayoutBuilder.Build(State.Rounds) : new BracketLayout();

        var gameType = State.ActiveTournament?.GameType;
        IsEightBallGame = gameType == GameType.EightBall;
        IsNineBallGame = gameType == GameType.NineBall;
        IsTenBallGame = gameType == GameType.TenBall;

        OnPropertyChanged(nameof(Bracket));
        OnPropertyChanged(nameof(IsEliminationBracket));
        OnPropertyChanged(nameof(ShowFlatRounds));
        OnPropertyChanged(nameof(IsEightBallGame));
        OnPropertyChanged(nameof(IsNineBallGame));
        OnPropertyChanged(nameof(IsTenBallGame));
    }

    public IEnumerable<TableAssignmentRow> TableAssignments =>
        State.Tables.Select(table => new TableAssignmentRow(table, FindCurrentMatch(table.Id)));

    private MatchRowViewModel? FindCurrentMatch(Guid tableId)
    {
        var match = State.ActiveTournament?.Matches
            .FirstOrDefault(m => m.TableId == tableId &&
                (m.Status == MatchStatus.Scheduled || m.Status == MatchStatus.InProgress));
        return match is null ? null : new MatchRowViewModel(match, State.ActiveTournament?.SeedingRatingSystem);
    }
}
