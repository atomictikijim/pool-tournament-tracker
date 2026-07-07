using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private readonly ChipGameService _chipGameService;

    public TournamentStateService State { get; }

    public ObservableCollection<PlayerSelectionItem> EntrantCandidates { get; } = new();

    /// <summary>Roster players not already entered in the active tournament, for the "Add Player" picker.</summary>
    public ObservableCollection<Player> AddablePlayers { get; } = new();

    [ObservableProperty]
    private Player? _selectedPlayerToAdd;

    /// <summary>
    /// True while the active tournament hasn't had any match/game actually played yet, so a new
    /// entrant can be added and the bracket/schedule safely regenerated from scratch.
    /// </summary>
    public bool CanAddEntrant { get; private set; }

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

    /// <summary>Number of tables to create up front. Required for every format except Ring Game.</summary>
    [ObservableProperty]
    private int _newTournamentTableCount = 4;

    [ObservableProperty]
    private decimal _newRingBuyIn = 20m;

    [ObservableProperty]
    private decimal _newRingFivePayout = 5m;

    [ObservableProperty]
    private decimal _newRingNinePayout = 10m;

    [ObservableProperty]
    private int _newChipStartingChips = 3;

    [ObservableProperty]
    private decimal _newChipBuyIn = 20m;

    [ObservableProperty]
    private decimal _newChipFirstPayout = 100m;

    [ObservableProperty]
    private decimal _newChipSecondPayout = 40m;

    [ObservableProperty]
    private decimal _newChipThirdPayout = 0m;

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>Winner/loser selected in the "record a game" pickers (entrant ids).</summary>
    [ObservableProperty]
    private Guid? _selectedChipWinnerId;

    [ObservableProperty]
    private Guid? _selectedChipLoserId;

    /// <summary>True while the create form has Ring Game selected, so ring-only fields can show.</summary>
    public bool IsCreatingRingGame => NewTournamentFormat == TournamentFormat.RingGame;

    /// <summary>True while the create form has Chip Tournament selected, so chip-only fields can show.</summary>
    public bool IsCreatingChipTournament => NewTournamentFormat == TournamentFormat.ChipTournament;

    /// <summary>Every format except Ring Game requires a table count before creating.</summary>
    public bool RequiresTableCount => NewTournamentFormat != TournamentFormat.RingGame;

    partial void OnNewTournamentFormatChanged(TournamentFormat value)
    {
        OnPropertyChanged(nameof(IsCreatingRingGame));
        OnPropertyChanged(nameof(IsCreatingChipTournament));
        OnPropertyChanged(nameof(RequiresTableCount));
    }

    public TournamentViewModel(
        ITournamentRepository tournamentRepository,
        IPlayerRepository playerRepository,
        BracketGenerationService bracketService,
        RoundRobinSchedulingService roundRobinService,
        RingGameService ringGameService,
        ChipGameService chipGameService,
        TournamentStateService state)
    {
        _tournamentRepository = tournamentRepository;
        _playerRepository = playerRepository;
        _bracketService = bracketService;
        _roundRobinService = roundRobinService;
        _ringGameService = ringGameService;
        _chipGameService = chipGameService;
        State = state;
        State.PropertyChanged += OnStateChanged;
        RebuildBracket();
    }

    // ---- Live bracket tree (editable) --------------------------------------------------------
    // Same tree layout as the read-only Display window, but with taller boxes so each match can
    // carry inline score inputs + a Report control. Rebuilt whenever the shared round data changes.
    private const double EditableBoxWidth = 250;
    private const double EditableBoxHeight = 108;
    private const double EditableRowGap = 18;

    /// <summary>The positioned bracket tree for elimination formats (empty otherwise).</summary>
    public BracketLayout Bracket { get; private set; } = new();

    /// <summary>True when the active tournament is a single/double-elimination bracket.</summary>
    public bool IsEliminationBracket { get; private set; }

    /// <summary>True for round-robin, which falls back to the simple round-column list.</summary>
    public bool ShowFlatRounds { get; private set; }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TournamentStateService.ActiveTournament)
            or nameof(TournamentStateService.Rounds))
        {
            RebuildBracket();
        }
    }

    private void RebuildBracket()
    {
        var format = State.ActiveTournament?.Format;
        IsEliminationBracket = format is TournamentFormat.SingleElimination or TournamentFormat.DoubleElimination;
        ShowFlatRounds = format is TournamentFormat.RoundRobin;
        Bracket = IsEliminationBracket
            ? BracketLayoutBuilder.Build(State.Rounds, EditableBoxWidth, EditableBoxHeight, EditableRowGap)
            : new BracketLayout();

        OnPropertyChanged(nameof(Bracket));
        OnPropertyChanged(nameof(IsEliminationBracket));
        OnPropertyChanged(nameof(ShowFlatRounds));
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
            await RefreshAddablePlayersAsync();
            RefreshCanAddEntrant();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load tournament: {ex.Message}";
        }
    }

    private async Task RefreshAddablePlayersAsync()
    {
        AddablePlayers.Clear();
        var tournament = State.ActiveTournament;
        if (tournament is null)
        {
            return;
        }

        var existingPlayerIds = tournament.Entrants.Select(e => e.PlayerId).ToHashSet();
        var players = await _playerRepository.GetAllAsync();
        foreach (var player in players.Where(p => !existingPlayerIds.Contains(p.Id)))
        {
            AddablePlayers.Add(player);
        }
    }

    private void RefreshCanAddEntrant()
    {
        CanAddEntrant = ComputeCanAddEntrant(State.ActiveTournament);
        OnPropertyChanged(nameof(CanAddEntrant));
    }

    private static bool ComputeCanAddEntrant(Tournament? tournament)
    {
        if (tournament is null)
        {
            return false;
        }

        return tournament.Format switch
        {
            TournamentFormat.RingGame => tournament.RingGame is not null
                && tournament.RingGame.LedgerEntries.All(l => l.Type == RingLedgerEntryType.BuyIn),
            TournamentFormat.ChipTournament => tournament.ChipGame is not null && tournament.ChipGame.Entries.Count == 0,
            _ => tournament.Matches.All(m => m.Status == MatchStatus.Scheduled)
        };
    }

    [RelayCommand]
    private async Task AddEntrantAsync()
    {
        var tournament = State.ActiveTournament;
        var player = SelectedPlayerToAdd;
        if (tournament is null || player is null || !CanAddEntrant)
        {
            return;
        }

        var newTotal = tournament.Entrants.Count + 1;
        if (tournament.Format == TournamentFormat.DoubleElimination && (newTotal & (newTotal - 1)) != 0)
        {
            StatusMessage = "Double elimination currently requires a power-of-2 number of entrants (2, 4, 8, 16, 32...).";
            return;
        }

        try
        {
            var newEntrant = new TournamentEntrant
            {
                TournamentId = tournament.Id,
                PlayerId = player.Id,
                Player = player
            };
            tournament.Entrants.Add(newEntrant);
            _tournamentRepository.TrackNew(newEntrant);

            switch (tournament.Format)
            {
                case TournamentFormat.RoundRobin:
                    RegenerateRoundRobin(tournament);
                    break;
                case TournamentFormat.SingleElimination:
                    RegenerateBracket(tournament, _bracketService.GenerateSingleElimination);
                    break;
                case TournamentFormat.DoubleElimination:
                    RegenerateBracket(tournament, _bracketService.GenerateDoubleElimination);
                    break;
                case TournamentFormat.RingGame:
                    AddRingEntrant(tournament, newEntrant);
                    break;
                case TournamentFormat.ChipTournament:
                    // No further state: ChipGameService derives standings/chips from the log
                    // alone, so an entrant with zero log entries already shows starting chips.
                    break;
            }

            await _tournamentRepository.SaveChangesAsync();

            // Reload rather than RebuildRounds() off the in-memory graph: the freshly generated
            // Match/BracketNode objects only have Player1EntrantId/Player2EntrantId set, not the
            // Player1Entrant/Player2Entrant navigation MatchRowViewModel reads names from - only a
            // full GetByIdAsync (with its Include chain) populates those.
            await State.SelectTournamentAsync(tournament.Id);
            await RefreshAddablePlayersAsync();
            RefreshCanAddEntrant();
            SelectedPlayerToAdd = null;
            StatusMessage = $"Added {player.FullName}.";
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void RegenerateRoundRobin(Tournament tournament)
    {
        foreach (var oldMatch in tournament.Matches.ToList())
        {
            _tournamentRepository.TrackRemoved(oldMatch);
        }
        tournament.Matches.Clear();

        SeedingService.AssignSeeds(tournament.Entrants, tournament.SeedingRatingSystem ?? RatingSystem.Fargo);
        _roundRobinService.GenerateSchedule(tournament);

        foreach (var newMatch in tournament.Matches)
        {
            _tournamentRepository.TrackNew(newMatch);
        }
    }

    private void RegenerateBracket(Tournament tournament, Func<Tournament, BracketDetail> generate)
    {
        if (tournament.Bracket is not null)
        {
            foreach (var node in tournament.Bracket.Nodes.ToList())
            {
                _tournamentRepository.TrackRemoved(node);
            }
            _tournamentRepository.TrackRemoved(tournament.Bracket);
            tournament.Bracket = null;
        }

        foreach (var oldMatch in tournament.Matches.ToList())
        {
            _tournamentRepository.TrackRemoved(oldMatch);
        }
        tournament.Matches.Clear();

        SeedingService.AssignSeeds(tournament.Entrants, tournament.SeedingRatingSystem ?? RatingSystem.Fargo);
        var bracket = generate(tournament);

        _tournamentRepository.TrackNew(bracket);
        foreach (var node in bracket.Nodes)
        {
            _tournamentRepository.TrackNew(node);
        }
        foreach (var match in tournament.Matches)
        {
            _tournamentRepository.TrackNew(match);
        }
    }

    private void AddRingEntrant(Tournament tournament, TournamentEntrant newEntrant)
    {
        var ringGame = tournament.RingGame!;
        newEntrant.SeedNumber = tournament.Entrants.Count;
        var entry = new RingLedgerEntry
        {
            RingGameDetailId = ringGame.Id,
            EntrantId = newEntrant.Id,
            Type = RingLedgerEntryType.BuyIn,
            Amount = ringGame.BuyInAmount,
            RackNumber = null,
            Sequence = ringGame.LedgerEntries.Count
        };
        ringGame.LedgerEntries.Add(entry);
        _tournamentRepository.TrackNew(entry);
    }

    [RelayCommand]
    private async Task CreateTournamentAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTournamentName))
        {
            StatusMessage = "Enter a tournament name.";
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

        if (NewTournamentFormat != TournamentFormat.RingGame && NewTournamentTableCount < 1)
        {
            StatusMessage = "Enter the number of available tables.";
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

        if (NewTournamentFormat != TournamentFormat.RingGame)
        {
            for (var i = 1; i <= NewTournamentTableCount; i++)
            {
                tournament.Tables.Add(new Table { TournamentId = tournament.Id, Label = $"Table {i}" });
            }
        }

        var missingRatingCount = 0;
        if (NewTournamentFormat == TournamentFormat.RingGame)
        {
            // Rotation order is a draw (the entrant selection order), not a rating seed.
            _ringGameService.StartRingGame(tournament, NewRingBuyIn, NewRingFivePayout, NewRingNinePayout);
        }
        else if (NewTournamentFormat == TournamentFormat.ChipTournament)
        {
            // Ad-hoc "loser loses a life" play; no seeding or pairings.
            _chipGameService.StartChipTournament(
                tournament, NewChipStartingChips, NewChipBuyIn, NewChipFirstPayout, NewChipSecondPayout, NewChipThirdPayout);
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
    private async Task StartMatchAsync(MatchRowViewModel? row)
    {
        if (row is null || State.ActiveTournament is null)
        {
            return;
        }

        var tournament = State.ActiveTournament;
        var match = row.Match;
        if (!row.IsStartable)
        {
            return;
        }

        if (tournament.Format != TournamentFormat.RingGame && tournament.Tables.Count == 0)
        {
            StatusMessage = "Add at least one table before starting matches.";
            return;
        }

        if (match.TableId is null)
        {
            StatusMessage = "Assign a table before starting this match.";
            return;
        }

        var inUseBy = tournament.Matches.FirstOrDefault(m =>
            m.Id != match.Id && m.TableId == match.TableId && m.Status == MatchStatus.InProgress);
        if (inUseBy is not null)
        {
            var tableLabel = tournament.Tables.FirstOrDefault(t => t.Id == match.TableId)?.Label ?? "That table";
            StatusMessage = $"{tableLabel} is already in use by another match.";
            return;
        }

        match.Status = MatchStatus.InProgress;
        match.StartedAtUtc = DateTime.UtcNow;
        await _tournamentRepository.SaveChangesAsync();
        State.RebuildRounds();
        RefreshCanAddEntrant();
    }

    [RelayCommand]
    private async Task FinishMatchAsync(MatchRowViewModel? row)
    {
        if (row is null || State.ActiveTournament is null)
        {
            return;
        }

        var tournament = State.ActiveTournament;
        var match = row.Match;
        if (match.Player1Score is null || match.Player2Score is null)
        {
            StatusMessage = "Enter both scores before finishing.";
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

            // Reload rather than RebuildRounds() off the in-memory graph: any newly-materialized
            // advancing Match only has Player1EntrantId/Player2EntrantId set, not the
            // Player1Entrant/Player2Entrant navigation MatchRowViewModel reads names from.
            await State.SelectTournamentAsync(tournament.Id);
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

    [RelayCommand]
    private async Task RecordChipGameAsync()
    {
        var tournament = State.ActiveTournament;
        if (tournament?.ChipGame is null)
        {
            return;
        }
        if (SelectedChipWinnerId is null || SelectedChipLoserId is null)
        {
            StatusMessage = "Pick both a winner and a loser.";
            return;
        }

        try
        {
            var loserName = tournament.Entrants.FirstOrDefault(e => e.Id == SelectedChipLoserId)?.Player?.FullName ?? "Player";
            var entry = _chipGameService.RecordGame(tournament, SelectedChipWinnerId.Value, SelectedChipLoserId.Value);
            _tournamentRepository.TrackNew(entry);
            await _tournamentRepository.SaveChangesAsync();

            SelectedChipWinnerId = null;
            SelectedChipLoserId = null;

            State.RebuildRounds();
            await RefreshTournamentSummaryAsync();

            if (tournament.Status == TournamentStatus.Completed)
            {
                var champion = ChipGameService.ComputeStandings(tournament).FirstOrDefault(r => r.Place == 1)?.Entrant.Player?.FullName ?? "Unknown player";
                StatusMessage = $"Chip tournament over - {champion} wins!";
            }
            else
            {
                StatusMessage = $"{loserName} loses a chip.";
            }
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
