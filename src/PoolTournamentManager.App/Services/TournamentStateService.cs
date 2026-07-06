using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PoolTournamentManager.App.ViewModels;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Interfaces;

namespace PoolTournamentManager.App.Services;

/// <summary>
/// Single shared source of truth for "what tournament is currently open and what does its
/// bracket/tables look like right now". Registered as a DI singleton and injected into both
/// the admin window's ViewModel and the read-only display window's ViewModel, so both windows
/// are bound to the exact same objects - a mutation from the admin side is visible on the
/// display side the instant WPF's binding engine re-renders, with no polling or messaging.
/// </summary>
public partial class TournamentStateService : ObservableObject
{
    private readonly ITournamentRepository _tournamentRepository;

    public ObservableCollection<Tournament> Tournaments { get; } = new();

    [ObservableProperty]
    private Tournament? _activeTournament;

    [ObservableProperty]
    private ObservableCollection<RoundGroupViewModel> _rounds = new();

    [ObservableProperty]
    private ObservableCollection<Table> _tables = new();

    public TournamentStateService(ITournamentRepository tournamentRepository)
    {
        _tournamentRepository = tournamentRepository;
    }

    public async Task LoadTournamentsAsync()
    {
        var tournaments = await _tournamentRepository.GetAllAsync();
        Tournaments.Clear();
        foreach (var tournament in tournaments)
        {
            Tournaments.Add(tournament);
        }
    }

    public async Task SelectTournamentAsync(Guid? tournamentId)
    {
        if (tournamentId is null)
        {
            ActiveTournament = null;
            Rounds = new ObservableCollection<RoundGroupViewModel>();
            Tables = new ObservableCollection<Table>();
            return;
        }

        ActiveTournament = await _tournamentRepository.GetByIdAsync(tournamentId.Value);
        Tables = new ObservableCollection<Table>(ActiveTournament?.Tables ?? new List<Table>());
        RebuildRounds();
    }

    public void RebuildRounds()
    {
        var rounds = new ObservableCollection<RoundGroupViewModel>();
        var bracket = ActiveTournament?.Bracket;
        if (bracket is null || bracket.Nodes.Count == 0)
        {
            Rounds = rounds;
            return;
        }

        var totalRounds = bracket.Nodes.Max(n => n.RoundNumber);
        var groups = bracket.Nodes
            .Where(n => n.MatchId is not null)
            .GroupBy(n => n.RoundNumber)
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            var matchRows = group
                .OrderBy(n => n.PositionInRound)
                .Select(n => new MatchRowViewModel(n.Match!))
                .ToList();

            var title = group.Key == totalRounds
                ? "Final"
                : group.Key == totalRounds - 1
                    ? "Semifinals"
                    : $"Round {group.Key}";

            rounds.Add(new RoundGroupViewModel(group.Key, title, matchRows));
        }

        Rounds = rounds;
    }

    /// <summary>
    /// Table entities are plain POCOs (no INotifyPropertyChanged), so a raw TableId edit on a
    /// Match doesn't naturally raise any change notification. Call this after persisting a
    /// table-assignment edit so bound displays (e.g. the "now playing" board) refresh.
    /// </summary>
    public void NotifyTableAssignmentsChanged()
    {
        Tables = new ObservableCollection<Table>(Tables);
    }
}
