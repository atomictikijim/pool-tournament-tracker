using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Interfaces;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.App.ViewModels;

public partial class TournamentViewModel : ObservableObject
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly BracketGenerationService _bracketService;

    public ObservableCollection<Tournament> Tournaments { get; } = new();
    public ObservableCollection<PlayerSelectionItem> EntrantCandidates { get; } = new();

    public IEnumerable<GameType> GameTypes { get; } = Enum.GetValues<GameType>();
    public IEnumerable<TournamentFormat> Formats { get; } = Enum.GetValues<TournamentFormat>();
    public IEnumerable<RatingSystem> RatingSystems { get; } = Enum.GetValues<RatingSystem>();

    [ObservableProperty]
    private Tournament? _selectedTournamentSummary;

    [ObservableProperty]
    private Tournament? _activeTournament;

    [ObservableProperty]
    private ObservableCollection<RoundGroupViewModel> _rounds = new();

    [ObservableProperty]
    private ObservableCollection<Table> _tables = new();

    [ObservableProperty]
    private string _newTournamentName = string.Empty;

    [ObservableProperty]
    private GameType _newTournamentGameType = GameType.EightBall;

    [ObservableProperty]
    private TournamentFormat _newTournamentFormat = TournamentFormat.SingleElimination;

    [ObservableProperty]
    private RatingSystem _newTournamentRatingSystem = RatingSystem.Fargo;

    [ObservableProperty]
    private string? _statusMessage;

    public TournamentViewModel(
        ITournamentRepository tournamentRepository,
        IPlayerRepository playerRepository,
        BracketGenerationService bracketService)
    {
        _tournamentRepository = tournamentRepository;
        _playerRepository = playerRepository;
        _bracketService = bracketService;
    }

    public async Task InitializeAsync()
    {
        await LoadTournamentsAsync();
        await LoadEntrantCandidatesAsync();
    }

    [RelayCommand]
    public async Task LoadTournamentsAsync()
    {
        var tournaments = await _tournamentRepository.GetAllAsync();
        Tournaments.Clear();
        foreach (var tournament in tournaments)
        {
            Tournaments.Add(tournament);
        }
    }

    /// <summary>
    /// Reloads the tournament summary list, e.g. after a status change, and re-points
    /// SelectedTournamentSummary at the same (identity-mapped) tournament so the ListBox
    /// selection survives the refresh instead of being cleared.
    /// </summary>
    private async Task RefreshTournamentSummaryAsync()
    {
        var selectedId = SelectedTournamentSummary?.Id;
        await LoadTournamentsAsync();
        if (selectedId is not null)
        {
            SelectedTournamentSummary = Tournaments.FirstOrDefault(t => t.Id == selectedId);
        }
    }

    [RelayCommand]
    public async Task LoadEntrantCandidatesAsync()
    {
        var players = await _playerRepository.GetAllAsync();
        EntrantCandidates.Clear();
        foreach (var player in players.Where(p => p.IsActive))
        {
            EntrantCandidates.Add(new PlayerSelectionItem(player));
        }
    }

    partial void OnSelectedTournamentSummaryChanged(Tournament? value)
    {
        _ = LoadActiveTournamentDetailAsync(value?.Id);
    }

    private async Task LoadActiveTournamentDetailAsync(Guid? tournamentId)
    {
        if (tournamentId is null)
        {
            ActiveTournament = null;
            Rounds = new ObservableCollection<RoundGroupViewModel>();
            Tables = new ObservableCollection<Table>();
            return;
        }

        try
        {
            ActiveTournament = await _tournamentRepository.GetByIdAsync(tournamentId.Value);
            Tables = new ObservableCollection<Table>(ActiveTournament?.Tables ?? new List<Table>());
            RebuildRounds();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load tournament: {ex.Message}";
        }
    }

    private void RebuildRounds()
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

    [RelayCommand]
    private async Task CreateTournamentAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTournamentName))
        {
            StatusMessage = "Enter a tournament name.";
            return;
        }

        if (NewTournamentFormat != TournamentFormat.SingleElimination)
        {
            StatusMessage = "Only single-elimination is supported in this version.";
            return;
        }

        var selected = EntrantCandidates.Where(c => c.IsSelected).ToList();
        if (selected.Count < 2)
        {
            StatusMessage = "Select at least 2 players.";
            return;
        }

        var tournament = new Tournament
        {
            Name = NewTournamentName,
            GameType = NewTournamentGameType,
            Format = NewTournamentFormat,
            SeedingRatingSystem = NewTournamentRatingSystem
        };

        foreach (var candidate in selected)
        {
            tournament.Entrants.Add(new TournamentEntrant
            {
                TournamentId = tournament.Id,
                PlayerId = candidate.Player.Id,
                Player = candidate.Player
            });
        }

        var missingRatingCount = tournament.Entrants.Count(e => !SeedingService.HasRating(e, NewTournamentRatingSystem));
        SeedingService.AssignSeeds(tournament.Entrants, NewTournamentRatingSystem);
        _bracketService.GenerateSingleElimination(tournament);

        await _tournamentRepository.AddAsync(tournament);

        StatusMessage = missingRatingCount > 0
            ? $"Created '{tournament.Name}' with {tournament.Entrants.Count} entrants ({missingRatingCount} missing a {NewTournamentRatingSystem} rating, seeded last)."
            : $"Created '{tournament.Name}' with {tournament.Entrants.Count} entrants.";

        NewTournamentName = string.Empty;
        foreach (var candidate in EntrantCandidates)
        {
            candidate.IsSelected = false;
        }

        await LoadTournamentsAsync();
        SelectedTournamentSummary = Tournaments.FirstOrDefault(t => t.Id == tournament.Id);
    }

    [RelayCommand]
    private async Task ReportResultAsync(MatchRowViewModel? row)
    {
        if (row is null || ActiveTournament is null)
        {
            return;
        }

        var match = row.Match;
        if (match.Player1Score is null || match.Player2Score is null)
        {
            StatusMessage = "Enter both scores before reporting.";
            return;
        }

        try
        {
            var newMatch = _bracketService.RecordMatchResult(ActiveTournament, match, match.Player1Score.Value, match.Player2Score.Value);
            if (newMatch is not null)
            {
                _tournamentRepository.TrackNew(newMatch);
            }
            await _tournamentRepository.SaveChangesAsync();
            RebuildRounds();
            await RefreshTournamentSummaryAsync();

            if (ActiveTournament.Status == TournamentStatus.Completed)
            {
                var championName = ActiveTournament.Entrants
                    .FirstOrDefault(e => e.Id == match.WinnerEntrantId)?.Player?.FullName ?? "Unknown player";
                StatusMessage = $"{championName} wins the tournament!";
            }
            else
            {
                StatusMessage = "Result recorded.";
            }
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AddTableAsync()
    {
        if (ActiveTournament is null)
        {
            return;
        }

        var table = new Table { TournamentId = ActiveTournament.Id, Label = $"Table {Tables.Count + 1}" };
        ActiveTournament.Tables.Add(table);
        Tables.Add(table);
        _tournamentRepository.TrackNew(table);
        await _tournamentRepository.SaveChangesAsync();
    }

    [RelayCommand]
    private async Task SaveAssignmentsAsync()
    {
        if (ActiveTournament is null)
        {
            return;
        }

        await _tournamentRepository.SaveChangesAsync();
        StatusMessage = "Table assignments saved.";
    }
}
