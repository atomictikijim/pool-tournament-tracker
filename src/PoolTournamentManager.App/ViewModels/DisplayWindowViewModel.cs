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
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TournamentStateService.ActiveTournament)
            or nameof(TournamentStateService.Tables)
            or nameof(TournamentStateService.Rounds))
        {
            OnPropertyChanged(nameof(TableAssignments));
        }
    }

    public IEnumerable<TableAssignmentRow> TableAssignments =>
        State.Tables.Select(table => new TableAssignmentRow(table, FindCurrentMatch(table.Id)));

    private MatchRowViewModel? FindCurrentMatch(Guid tableId)
    {
        var match = State.ActiveTournament?.Matches
            .FirstOrDefault(m => m.TableId == tableId && m.Status == MatchStatus.Scheduled);
        return match is null ? null : new MatchRowViewModel(match);
    }
}
