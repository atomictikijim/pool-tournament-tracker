using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoolTournamentManager.App.Services;
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
    private readonly RoundRobinSchedulingService _roundRobinService;
    private readonly RingGameService _ringGameService;

    public TournamentStateService State { get; }

    public ObservableCollection<PlayerSelectionItem> EntrantCandidates { get; } = new();

    public IEnumerable<GameType> GameTypes { get; } = Enum.GetValues<GameType>();
    public IEnumerable<TournamentFormat> Formats { get; } = Enum.GetValues<TournamentFormat>();
    public IEnumerable<RatingSystem> RatingSystems { get; } = Enum.GetValues<RatingSystem>();

    [ObservableProperty]
    private Tournament? _selectedTournamentSummary;

    [ObservableProperty]
    private string _newTournamentName = string.Empty;

    [ObservableProperty]
    private GameType _newTournamentGameType = GameType.EightBall;

    [ObservableProperty]
    private TournamentFormat _newTournamentFormat = TournamentFormat.SingleElimination;

    [ObservableProperty]
    private RatingSystem _newTournamentRatingSystem = RatingSystem.Fargo;

    [ObservableProperty]
    private decimal _newRingBuyIn = 20m;

    [ObservableProperty]
    private decimal _newRingFivePayout = 5m;

    [ObservableProperty]
    private decimal _newRingNinePayout = 10m;

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>True while the create form has Ring Game selected, so ring-only fields can show.</summary>
    public bool IsCreatingRingGame => NewTournamentFormat == TournamentFormat.RingGame;

    partial void OnNewTournamentFormatChanged(TournamentFormat value) => OnPropertyChanged(nameof(IsCreatingRingGame));

    public TournamentViewModel(
        ITournamentRepository tournamentRepository,
        IPlayerRepository playerRepository,
        BracketGenerationService bracketService,
        RoundRobinSchedulingService roundRobinService,
        RingGameService ringGameService,
        TournamentStateService state)
    {
        _tournamentRepository = tournamentRepository;
        _playerRepository = playerRepository;
        _bracketService = bracketService;
        _roundRobinService = roundRobinService;
        _ringGameService = ringGameService;
        State = state;
    }

    public async Task InitializeAsync()
    {
        await State.LoadTournamentsAsync();
        await LoadEntrantCandidatesAsync();
    }

    [RelayCommand]
    public async Task LoadTournamentsAsync() => await State.LoadTournamentsAsync();

    /// <summary>
    /// Reloads the tournament summary list, e.g. after a status change, and re-points
    /// SelectedTournamentSummary at the same (identity-mapped) tournament so the ListBox
    /// selection survives the refresh instead of being cleared.
    /// </summary>
    private async Task RefreshTournamentSummaryAsync()
    {
        var selectedId = SelectedTournamentSummary?.Id;
        await State.LoadTournamentsAsync();
        if (selectedId is not null)
        {
            SelectedTournamentSummary = State.Tournaments.FirstOrDefault(t => t.Id == selectedId);
        }
    }

    [RelayCommand]
    public async Task LoadEntrantCandidatesAsync()
    {
        var players = await _playerRepository.GetAllAsync();
        EntrantCandidates.Clear();
        foreach (var player in players)
        {
            EntrantCandidates.Add(new PlayerSelectionItem(player));
        }
    }

    partial void OnSelectedTournamentSummaryChanged(Tournament? value)
    {
        _ = SelectTournamentAsync(value?.Id);
    }

    private async Task SelectTournamentAsync(Guid? tournamentId)
    {
        try
        {
            await State.SelectTournamentAsync(tournamentId);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load tournament: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CreateTournamentAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTournamentName))
        {
            StatusMessage = "Enter a tournament name.";
            return;
        }

        if (NewTournamentFormat == TournamentFormat.ChipTournament)
        {
            StatusMessage = "Chip tournaments aren't supported yet.";
            return;
        }

        var selected = EntrantCandidates.Where(c => c.IsSelected).ToList();
        if (selected.Count < 2)
        {
            StatusMessage = "Select at least 2 players.";
            return;
        }

        if (NewTournamentFormat == TournamentFormat.DoubleElimination && (selected.Count & (selected.Count - 1)) != 0)
        {
            StatusMessage = "Double elimination currently requires a power-of-2 number of entrants (2, 4, 8, 16, 32...).";
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

        var missingRatingCount = 0;
        if (NewTournamentFormat == TournamentFormat.RingGame)
        {
            // Rotation order is a draw (the entrant selection order), not a rating seed.
            _ringGameService.StartRingGame(tournament, NewRingBuyIn, NewRingFivePayout, NewRingNinePayout);
        }
        else
        {
            missingRatingCount = tournament.Entrants.Count(e => !SeedingService.HasRating(e, NewTournamentRatingSystem));
            SeedingService.AssignSeeds(tournament.Entrants, NewTournamentRatingSystem);

            if (NewTournamentFormat == TournamentFormat.DoubleElimination)
            {
                _bracketService.GenerateDoubleElimination(tournament);
            }
            else if (NewTournamentFormat == TournamentFormat.RoundRobin)
            {
                _roundRobinService.GenerateSchedule(tournament);
            }
            else
            {
                _bracketService.GenerateSingleElimination(tournament);
            }
        }

        await _tournamentRepository.AddAsync(tournament);

        StatusMessage = missingRatingCount > 0
            ? $"Created '{tournament.Name}' with {tournament.Entrants.Count} entrants ({missingRatingCount} missing a {NewTournamentRatingSystem} rating, seeded last)."
            : $"Created '{tournament.Name}' with {tournament.Entrants.Count} entrants.";

        NewTournamentName = string.Empty;
        foreach (var candidate in EntrantCandidates)
        {
            candidate.IsSelected = false;
        }

        await State.LoadTournamentsAsync();
        SelectedTournamentSummary = State.Tournaments.FirstOrDefault(t => t.Id == tournament.Id);
    }

    [RelayCommand]
    private async Task ReportResultAsync(MatchRowViewModel? row)
    {
        if (row is null || State.ActiveTournament is null)
        {
            return;
        }

        var tournament = State.ActiveTournament;
        var match = row.Match;
        if (match.Player1Score is null || match.Player2Score is null)
        {
            StatusMessage = "Enter both scores before reporting.";
            return;
        }

        try
        {
            var newMatches = _bracketService.RecordMatchResult(tournament, match, match.Player1Score.Value, match.Player2Score.Value);
            foreach (var newMatch in newMatches)
            {
                _tournamentRepository.TrackNew(newMatch);
            }

            if (tournament.Format == TournamentFormat.RoundRobin && tournament.Matches.All(m => m.Status == MatchStatus.Completed))
            {
                tournament.Status = TournamentStatus.Completed;
            }

            await _tournamentRepository.SaveChangesAsync();
            State.RebuildRounds();
            await RefreshTournamentSummaryAsync();

            if (tournament.Status == TournamentStatus.Completed)
            {
                var championName = tournament.Format == TournamentFormat.RoundRobin
                    ? RoundRobinStandingsService.ComputeStandings(tournament).FirstOrDefault()?.Entrant.Player?.FullName ?? "Unknown player"
                    : tournament.Entrants.FirstOrDefault(e => e.Id == match.WinnerEntrantId)?.Player?.FullName ?? "Unknown player";
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
        if (State.ActiveTournament is null)
        {
            return;
        }

        var table = new Table { TournamentId = State.ActiveTournament.Id, Label = $"Table {State.Tables.Count + 1}" };
        State.ActiveTournament.Tables.Add(table);
        State.Tables.Add(table);
        _tournamentRepository.TrackNew(table);
        await _tournamentRepository.SaveChangesAsync();
    }

    [RelayCommand]
    private async Task SaveAssignmentsAsync()
    {
        if (State.ActiveTournament is null)
        {
            return;
        }

        await _tournamentRepository.SaveChangesAsync();
        State.NotifyTableAssignmentsChanged();
        StatusMessage = "Table assignments saved.";
    }

    [RelayCommand]
    private Task RecordFiveBallAsync() => RecordMoneyBallAsync(RingMoneyBall.Five);

    [RelayCommand]
    private Task RecordNineBallAsync() => RecordMoneyBallAsync(RingMoneyBall.Nine);

    private async Task RecordMoneyBallAsync(RingMoneyBall ball)
    {
        var tournament = State.ActiveTournament;
        var shooterId = tournament?.RingGame?.CurrentShooterEntrantId;
        if (tournament is null || shooterId is null)
        {
            return;
        }

        try
        {
            var entry = _ringGameService.RecordMoneyBall(tournament, shooterId.Value, ball);
            _tournamentRepository.TrackNew(entry);
            await _tournamentRepository.SaveChangesAsync();
            State.RebuildRounds();
            var who = tournament.Entrants.FirstOrDefault(e => e.Id == shooterId)?.Player?.FullName ?? "Player";
            StatusMessage = $"{who} made the {(ball == RingMoneyBall.Nine ? "9" : "5")}.";
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AdvanceShooterAsync()
    {
        var tournament = State.ActiveTournament;
        if (tournament?.RingGame is null)
        {
            return;
        }

        try
        {
            _ringGameService.AdvanceShooter(tournament);
            await _tournamentRepository.SaveChangesAsync();
            State.RebuildRounds();
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CashOutAsync(RingSeatViewModel? seat)
    {
        var tournament = State.ActiveTournament;
        if (seat is null || tournament?.RingGame is null)
        {
            return;
        }

        try
        {
            var entry = _ringGameService.CashOut(tournament, seat.EntrantId);
            _tournamentRepository.TrackNew(entry);
            await _tournamentRepository.SaveChangesAsync();
            State.RebuildRounds();
            await RefreshTournamentSummaryAsync();

            if (tournament.Status == TournamentStatus.Completed)
            {
                var winner = RingGameService.ComputeStandings(tournament).FirstOrDefault()?.Entrant.Player?.FullName ?? "Unknown player";
                StatusMessage = $"Ring game over - {winner} finishes on top!";
            }
            else
            {
                StatusMessage = $"{seat.PlayerName} cashed out at {seat.NetDisplay}.";
            }
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
