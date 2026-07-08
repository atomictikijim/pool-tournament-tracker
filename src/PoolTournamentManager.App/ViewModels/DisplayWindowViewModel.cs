using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PoolTournamentManager.App.Services;
using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.App.ViewModels;

/// <summary>
/// Read-only by construction: this ViewModel exposes no ICommand mutators, only projections
/// over the shared TournamentStateService, so there is nothing here for a bound control to
/// invoke that would change tournament state from the display window.
/// </summary>
public class DisplayWindowViewModel : ObservableObject
{
    public TournamentStateService State { get; }

    public DisplayWindowViewModel(TournamentStateService state)
    {
        State = state;
        State.PropertyChanged += OnStateChanged;
        RebuildBracket();
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

    private void RebuildBracket()
    {
        var format = State.ActiveTournament?.Format;
        IsEliminationBracket = format is TournamentFormat.SingleElimination or TournamentFormat.DoubleElimination
            or TournamentFormat.ModifiedSingleElimination;
        ShowFlatRounds = format is TournamentFormat.RoundRobin;
        Bracket = IsEliminationBracket ? BracketLayoutBuilder.Build(State.Rounds) : new BracketLayout();

        OnPropertyChanged(nameof(Bracket));
        OnPropertyChanged(nameof(IsEliminationBracket));
        OnPropertyChanged(nameof(ShowFlatRounds));
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
