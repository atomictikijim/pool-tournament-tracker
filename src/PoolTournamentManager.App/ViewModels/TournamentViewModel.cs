using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
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
    private readonly ITeamRepository _teamRepository;
    private readonly BracketGenerationService _bracketService;
    private readonly RoundRobinSchedulingService _roundRobinService;
    private readonly RingGameService _ringGameService;
    private readonly ChipGameService _chipGameService;

    public TournamentStateService State { get; }

    public ObservableCollection<PlayerSelectionItem> EntrantCandidates { get; } = new();

    public ObservableCollection<TeamSelectionItem> TeamCandidates { get; } = new();

    /// <summary>Sentinel item meaning "don't filter by this field" in the Division/Location
    /// filter drop-downs.</summary>
    public const string AllFilterOption = "(All)";

    // Options for the Tournament tab's status filter. Only In Progress and Completed occur in
    // practice (a tournament goes straight to InProgress on creation and to Completed when it
    // finishes), so those two plus an "All" pass-through are all the filter needs.
    public const string StatusFilterAll = "All";
    public const string StatusFilterInProgress = "In Progress";
    public const string StatusFilterCompleted = "Completed";

    /// <summary>Choices for the tournament-list status filter on the Tournament tab.</summary>
    public ObservableCollection<string> AvailableStatusFilters { get; } =
        new() { StatusFilterAll, StatusFilterInProgress, StatusFilterCompleted };

    /// <summary>Distinct Division values currently in TeamCandidates, for the Division filter
    /// drop-down, plus the leading "(All)" option.</summary>
    public ObservableCollection<string> AvailableDivisionFilters { get; } = new() { AllFilterOption };

    /// <summary>Distinct Location values currently in TeamCandidates, for the Location filter
    /// drop-down, plus the leading "(All)" option.</summary>
    public ObservableCollection<string> AvailableLocationFilters { get; } = new() { AllFilterOption };

    private ICollectionView? _entrantCandidatesView;
    private ICollectionView? _teamCandidatesView;
    private ICollectionView? _tournamentsView;

    /// <summary>Filters the Tournament tab's list by status ("All" / "In Progress" / "Completed").</summary>
    [ObservableProperty]
    private string _tournamentStatusFilter = StatusFilterAll;

    /// <summary>The live filtered view of <see cref="EntrantCandidates"/> - the same view WPF's
    /// default binding uses, exposed so filter behavior can be verified without a running UI.</summary>
    public ICollectionView EntrantCandidatesView => _entrantCandidatesView!;

    /// <summary>The live filtered view of <see cref="TeamCandidates"/> - see <see cref="EntrantCandidatesView"/>.</summary>
    public ICollectionView TeamCandidatesView => _teamCandidatesView!;

    /// <summary>The live status-filtered view of the tournament list - see <see cref="EntrantCandidatesView"/>.</summary>
    public ICollectionView TournamentsView => _tournamentsView!;

    /// <summary>Filters the Entrants checklist by player name (substring, case-insensitive).</summary>
    [ObservableProperty]
    private string? _entrantNameFilter;

    /// <summary>Filters the Entrants checklist to players whose current rating (see
    /// <see cref="NewTournamentRatingSystem"/>) is at least this value. Players with no rating in
    /// that system are hidden while a min/max filter is active.</summary>
    [ObservableProperty]
    private int? _entrantMinRating;

    [ObservableProperty]
    private int? _entrantMaxRating;

    /// <summary>Filters the Teams checklist by team name (substring, case-insensitive).</summary>
    [ObservableProperty]
    private string? _teamNameFilter;

    [ObservableProperty]
    private string _teamDivisionFilter = AllFilterOption;

    [ObservableProperty]
    private string _teamLocationFilter = AllFilterOption;

    /// <summary>"Fargo rating" / "APA 8-Ball rating" etc., for the min/max filter's label.</summary>
    public string EntrantRatingFilterLabel => $"{SeedingService.GetRatingLabel(NewTournamentRatingSystem)} rating";

    /// <summary>Roster players not already entered in the active tournament, for the "Add Player" picker.</summary>
    public ObservableCollection<Player> AddablePlayers { get; } = new();

    /// <summary>Roster teams not already entered in the active tournament, for the "Add Team" picker.</summary>
    public ObservableCollection<Team> AddableTeams { get; } = new();

    [ObservableProperty]
    private Player? _selectedPlayerToAdd;

    [ObservableProperty]
    private Team? _selectedTeamToAdd;

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

    /// <summary>Whether the tournament being created uses Team entrants instead of individual Players.
    /// Only offered for Single/Double Elimination - see <see cref="IsTeamEligibleFormat"/>.</summary>
    [ObservableProperty]
    private bool _useTeams;

    [ObservableProperty]
    private decimal _newRingBuyIn = 20m;

    [ObservableProperty]
    private decimal _newRingFivePayout = 5m;

    [ObservableProperty]
    private decimal _newRingNinePayout = 10m;

    [ObservableProperty]
    private int _newChipStartingChips = 3;

    /// <summary>Per-entrant entry fee. Shown for every format except Ring Game, which has its
    /// own separate buy-in - see <see cref="ShowEntryFeeSection"/>.</summary>
    [ObservableProperty]
    private decimal _newEntryFee;

    /// <summary>Percentage of total entry fees the tournament host keeps.</summary>
    [ObservableProperty]
    private decimal _newHostFeePercentage;

    /// <summary>Number of finishing places that receive a prize-pool payout. 0 = no payouts
    /// configured. Resizes <see cref="NewPrizePlaceInputs"/> when changed.</summary>
    [ObservableProperty]
    private int _newPayoutPlaceCount;

    /// <summary>One "Place N: __ %" row per configured payout place.</summary>
    public ObservableCollection<PrizePlaceInputViewModel> NewPrizePlaceInputs { get; } = new();

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

    /// <summary>Only Single/Double/Modified Single Elimination can be run with Team entrants.</summary>
    public bool IsTeamEligibleFormat => NewTournamentFormat is TournamentFormat.SingleElimination
        or TournamentFormat.DoubleElimination or TournamentFormat.ModifiedSingleElimination;

    /// <summary>True while the create form should show the Player checklist/rating controls
    /// instead of the Team checklist (i.e. UseTeams is off).</summary>
    public bool ShowPlayerEntrants => !UseTeams;

    /// <summary>Modified Single Elimination draws round 1 at random - the "seed by rating"
    /// control doesn't apply and is hidden for this format.</summary>
    public bool UsesRandomDraw => NewTournamentFormat == TournamentFormat.ModifiedSingleElimination;

    /// <summary>True while the "seed by rating" control should show.</summary>
    public bool ShowSeedByRating => ShowPlayerEntrants && !UsesRandomDraw;

    /// <summary>True for every format except Ring Game, which has its own separate buy-in/payout
    /// model with no discrete finishing order - see PrizePayoutService.</summary>
    public bool ShowEntryFeeSection => NewTournamentFormat != TournamentFormat.RingGame;

    /// <summary>Live "money that has come in" preview: entry fee times the currently-selected
    /// entrant count (Players or Teams, whichever checklist is active).</summary>
    public string TotalEntryFeesDisplay
    {
        get
        {
            var count = UseTeams
                ? TeamCandidates.Count(c => c.IsSelected)
                : EntrantCandidates.Count(c => c.IsSelected);
            return (NewEntryFee * count).ToString("C0");
        }
    }

    /// <summary>Live sum of the configured prize-place percentages, for the "Total: XX%" hint.</summary>
    public decimal PrizePlacePercentageTotal => NewPrizePlaceInputs.Sum(p => p.Percentage);

    /// <summary>True when no payout places are configured, or their percentages sum to 100.</summary>
    public bool IsPrizePlacePercentageValid =>
        NewPayoutPlaceCount == 0 || Math.Abs(PrizePlacePercentageTotal - 100m) < 0.01m;

    partial void OnNewTournamentFormatChanged(TournamentFormat value)
    {
        OnPropertyChanged(nameof(IsCreatingRingGame));
        OnPropertyChanged(nameof(IsCreatingChipTournament));
        OnPropertyChanged(nameof(RequiresTableCount));
        OnPropertyChanged(nameof(IsTeamEligibleFormat));
        OnPropertyChanged(nameof(UsesRandomDraw));
        OnPropertyChanged(nameof(ShowSeedByRating));
        OnPropertyChanged(nameof(ShowEntryFeeSection));
        if (!IsTeamEligibleFormat)
        {
            UseTeams = false;
        }
        RefreshEntrantCandidateRatings();
    }

    partial void OnUseTeamsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowPlayerEntrants));
        OnPropertyChanged(nameof(ShowSeedByRating));
        OnPropertyChanged(nameof(TotalEntryFeesDisplay));
        RefreshEntrantCandidateRatings();
    }

    partial void OnNewTournamentRatingSystemChanged(RatingSystem value)
    {
        RefreshEntrantCandidateRatings();
        OnPropertyChanged(nameof(EntrantRatingFilterLabel));
        _entrantCandidatesView?.Refresh();
    }

    /// <summary>Pushes the currently-selected "Seed by rating" system (or null while that control
    /// is hidden/inapplicable) onto every entrant candidate, so the checklist label shows the
    /// matching rating - see PlayerSelectionItem.DisplayLabel.</summary>
    private void RefreshEntrantCandidateRatings()
    {
        var ratingSystem = ShowSeedByRating ? NewTournamentRatingSystem : (RatingSystem?)null;
        foreach (var candidate in EntrantCandidates)
        {
            candidate.RatingSystem = ratingSystem;
        }
    }

    partial void OnNewEntryFeeChanged(decimal value) => OnPropertyChanged(nameof(TotalEntryFeesDisplay));

    partial void OnNewPayoutPlaceCountChanged(int value)
    {
        var target = Math.Max(0, value);
        while (NewPrizePlaceInputs.Count < target)
        {
            var row = new PrizePlaceInputViewModel(NewPrizePlaceInputs.Count + 1);
            row.PropertyChanged += OnPrizePlaceInputChanged;
            NewPrizePlaceInputs.Add(row);
        }
        while (NewPrizePlaceInputs.Count > target)
        {
            var last = NewPrizePlaceInputs[^1];
            last.PropertyChanged -= OnPrizePlaceInputChanged;
            NewPrizePlaceInputs.RemoveAt(NewPrizePlaceInputs.Count - 1);
        }

        OnPropertyChanged(nameof(PrizePlacePercentageTotal));
        OnPropertyChanged(nameof(IsPrizePlacePercentageValid));
    }

    private void OnPrizePlaceInputChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PrizePlaceInputViewModel.Percentage))
        {
            OnPropertyChanged(nameof(PrizePlacePercentageTotal));
            OnPropertyChanged(nameof(IsPrizePlacePercentageValid));
        }
    }

    public TournamentViewModel(
        ITournamentRepository tournamentRepository,
        IPlayerRepository playerRepository,
        ITeamRepository teamRepository,
        BracketGenerationService bracketService,
        RoundRobinSchedulingService roundRobinService,
        RingGameService ringGameService,
        ChipGameService chipGameService,
        TournamentStateService state)
    {
        _tournamentRepository = tournamentRepository;
        _playerRepository = playerRepository;
        _teamRepository = teamRepository;
        _bracketService = bracketService;
        _roundRobinService = roundRobinService;
        _ringGameService = ringGameService;
        _chipGameService = chipGameService;
        State = state;
        State.PropertyChanged += OnStateChanged;
        RebuildBracket();

        _entrantCandidatesView = CollectionViewSource.GetDefaultView(EntrantCandidates);
        _entrantCandidatesView.Filter = FilterEntrantCandidate;
        _teamCandidatesView = CollectionViewSource.GetDefaultView(TeamCandidates);
        _teamCandidatesView.Filter = FilterTeamCandidate;

        // The Tournament tab's ListBox binds State.Tournaments; filtering its default view (the same
        // view the ListBox uses) hides rows by status without touching the underlying collection, so
        // a reload (Clear/Add in LoadTournamentsAsync) re-applies the filter automatically.
        _tournamentsView = CollectionViewSource.GetDefaultView(State.Tournaments);
        _tournamentsView.Filter = FilterTournament;
    }

    private bool FilterTournament(object obj)
    {
        if (obj is not Tournament tournament)
        {
            return true;
        }

        return TournamentStatusFilter switch
        {
            StatusFilterInProgress => tournament.Status == TournamentStatus.InProgress,
            StatusFilterCompleted => tournament.Status == TournamentStatus.Completed,
            _ => true,
        };
    }

    partial void OnTournamentStatusFilterChanged(string value) => _tournamentsView?.Refresh();

    private bool FilterEntrantCandidate(object obj)
    {
        if (obj is not PlayerSelectionItem item)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(EntrantNameFilter) &&
            !item.Player.FullName.Contains(EntrantNameFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (EntrantMinRating is not null || EntrantMaxRating is not null)
        {
            var rating = SeedingService.GetRatingValue(item.Player, NewTournamentRatingSystem);
            if (rating is null)
            {
                return false;
            }
            if (EntrantMinRating is not null && rating < EntrantMinRating)
            {
                return false;
            }
            if (EntrantMaxRating is not null && rating > EntrantMaxRating)
            {
                return false;
            }
        }

        return true;
    }

    private bool FilterTeamCandidate(object obj)
    {
        if (obj is not TeamSelectionItem item)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(TeamNameFilter) &&
            !item.Team.Name.Contains(TeamNameFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TeamDivisionFilter != AllFilterOption &&
            !string.Equals(item.Team.Division, TeamDivisionFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TeamLocationFilter != AllFilterOption &&
            !string.Equals(item.Team.Location, TeamLocationFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    partial void OnEntrantNameFilterChanged(string? value) => _entrantCandidatesView?.Refresh();
    partial void OnEntrantMinRatingChanged(int? value) => _entrantCandidatesView?.Refresh();
    partial void OnEntrantMaxRatingChanged(int? value) => _entrantCandidatesView?.Refresh();
    partial void OnTeamNameFilterChanged(string? value) => _teamCandidatesView?.Refresh();
    partial void OnTeamDivisionFilterChanged(string value) => _teamCandidatesView?.Refresh();
    partial void OnTeamLocationFilterChanged(string value) => _teamCandidatesView?.Refresh();

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
        IsEliminationBracket = format is TournamentFormat.SingleElimination or TournamentFormat.DoubleElimination
            or TournamentFormat.ModifiedSingleElimination;
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
        await LoadTeamCandidatesAsync();
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
            var item = new PlayerSelectionItem(player);
            item.PropertyChanged += OnEntrantCandidateSelectionChanged;
            EntrantCandidates.Add(item);
        }
        RefreshEntrantCandidateRatings();
    }

    [RelayCommand]
    public async Task LoadTeamCandidatesAsync()
    {
        var teams = await _teamRepository.GetAllAsync();
        TeamCandidates.Clear();
        foreach (var team in teams)
        {
            var item = new TeamSelectionItem(team);
            item.PropertyChanged += OnEntrantCandidateSelectionChanged;
            TeamCandidates.Add(item);
        }

        AvailableDivisionFilters.Clear();
        AvailableDivisionFilters.Add(AllFilterOption);
        foreach (var division in teams.Select(t => t.Division).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().OrderBy(d => d))
        {
            AvailableDivisionFilters.Add(division!);
        }

        AvailableLocationFilters.Clear();
        AvailableLocationFilters.Add(AllFilterOption);
        foreach (var location in teams.Select(t => t.Location).Where(l => !string.IsNullOrWhiteSpace(l)).Distinct().OrderBy(l => l))
        {
            AvailableLocationFilters.Add(location!);
        }

        // Clearing/rebuilding the two lists above resets each ComboBox's SelectedItem to null
        // (a Reset notification deselects everything), which would otherwise leave the
        // Division/Location filters stuck excluding every team. Re-assert the "no filter"
        // default now that both lists are back in a valid state.
        TeamDivisionFilter = AllFilterOption;
        TeamLocationFilter = AllFilterOption;
    }

    private void OnEntrantCandidateSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerSelectionItem.IsSelected))
        {
            OnPropertyChanged(nameof(TotalEntryFeesDisplay));
        }
    }

    partial void OnSelectedTournamentSummaryChanged(Tournament? value)
    {
        _ = SelectTournamentAsync(value?.Id);
    }

    /// <summary>True when the active tournament's entrants are Teams, for the post-creation "Add" picker.</summary>
    public bool ActiveTournamentUsesTeams => State.ActiveTournament?.UsesTeams ?? false;

    /// <summary>True when the active tournament's entrants are individual Players (the default).</summary>
    public bool ActiveTournamentUsesPlayers => !ActiveTournamentUsesTeams;

    private async Task SelectTournamentAsync(Guid? tournamentId)
    {
        try
        {
            await State.SelectTournamentAsync(tournamentId);
            await RefreshAddablePlayersAsync();
            await RefreshAddableTeamsAsync();
            OnPropertyChanged(nameof(ActiveTournamentUsesTeams));
            OnPropertyChanged(nameof(ActiveTournamentUsesPlayers));
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

    private async Task RefreshAddableTeamsAsync()
    {
        AddableTeams.Clear();
        var tournament = State.ActiveTournament;
        if (tournament is null)
        {
            return;
        }

        var existingTeamIds = tournament.Entrants.Select(e => e.TeamId).ToHashSet();
        var teams = await _teamRepository.GetAllAsync();
        foreach (var team in teams.Where(t => !existingTeamIds.Contains(t.Id)))
        {
            AddableTeams.Add(team);
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
        if (tournament is null || !CanAddEntrant)
        {
            return;
        }

        TournamentEntrant newEntrant;
        string addedName;
        if (tournament.UsesTeams)
        {
            var team = SelectedTeamToAdd;
            if (team is null)
            {
                return;
            }
            newEntrant = new TournamentEntrant { TournamentId = tournament.Id, TeamId = team.Id, Team = team };
            addedName = team.Name;
        }
        else
        {
            var player = SelectedPlayerToAdd;
            if (player is null)
            {
                return;
            }
            newEntrant = new TournamentEntrant { TournamentId = tournament.Id, PlayerId = player.Id, Player = player };
            addedName = player.FullName;
        }

        var newTotal = tournament.Entrants.Count + 1;
        if (tournament.Format == TournamentFormat.DoubleElimination && (newTotal & (newTotal - 1)) != 0)
        {
            StatusMessage = "Double elimination currently requires a power-of-2 number of entrants (2, 4, 8, 16, 32...).";
            return;
        }

        if (tournament.Format == TournamentFormat.ModifiedSingleElimination && !BracketGenerationService.IsValidModifiedSingleEliminationCount(newTotal))
        {
            StatusMessage = "Modified Single Elimination currently requires a multiple-of-8 power-of-2 entrant count (8, 16, 32, 64...).";
            return;
        }

        try
        {
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
                case TournamentFormat.ModifiedSingleElimination:
                    RegenerateBracket(tournament, _bracketService.GenerateModifiedSingleElimination);
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
            await RefreshAddableTeamsAsync();
            RefreshCanAddEntrant();
            SelectedPlayerToAdd = null;
            SelectedTeamToAdd = null;
            StatusMessage = $"Added {addedName}.";
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

        var useTeams = UseTeams && IsTeamEligibleFormat;
        var selectedPlayers = EntrantCandidates.Where(c => c.IsSelected).ToList();
        var selectedTeams = TeamCandidates.Where(c => c.IsSelected).ToList();
        var entrantCount = useTeams ? selectedTeams.Count : selectedPlayers.Count;

        if (entrantCount < 2)
        {
            StatusMessage = useTeams ? "Select at least 2 teams." : "Select at least 2 players.";
            return;
        }

        // Double Elimination accepts any count >= 2 - the bracket pads to the next power of two and
        // the top seeds get first-round byes (see BracketGenerationService.GenerateDoubleElimination).

        if (NewTournamentFormat == TournamentFormat.ModifiedSingleElimination && !BracketGenerationService.IsValidModifiedSingleEliminationCount(entrantCount))
        {
            StatusMessage = "Modified Single Elimination requires at least 8 entrants.";
            return;
        }

        if (NewTournamentFormat != TournamentFormat.RingGame && NewTournamentTableCount < 1)
        {
            StatusMessage = "Enter the number of available tables.";
            return;
        }

        if (ShowEntryFeeSection)
        {
            if (NewEntryFee < 0)
            {
                StatusMessage = "Entry fee can't be negative.";
                return;
            }
            if (NewHostFeePercentage < 0 || NewHostFeePercentage > 100)
            {
                StatusMessage = "Host fee percentage must be between 0 and 100.";
                return;
            }
            if (!IsPrizePlacePercentageValid)
            {
                StatusMessage = $"Prize place percentages must add up to 100% (currently {PrizePlacePercentageTotal:0.##}%).";
                return;
            }
        }

        var tournament = new Tournament
        {
            Name = NewTournamentName,
            GameType = NewTournamentGameType,
            Format = NewTournamentFormat,
            UsesTeams = useTeams
        };

        if (ShowEntryFeeSection)
        {
            tournament.EntryFee = NewEntryFee;
            tournament.HostFeePercentage = NewHostFeePercentage;
            foreach (var place in NewPrizePlaceInputs)
            {
                tournament.PrizePlaces.Add(new TournamentPrizePlace
                {
                    TournamentId = tournament.Id,
                    Place = place.Place,
                    Percentage = place.Percentage
                });
            }
        }

        if (useTeams)
        {
            foreach (var candidate in selectedTeams)
            {
                tournament.Entrants.Add(new TournamentEntrant
                {
                    TournamentId = tournament.Id,
                    TeamId = candidate.Team.Id,
                    Team = candidate.Team
                });
            }
        }
        else
        {
            foreach (var candidate in selectedPlayers)
            {
                tournament.Entrants.Add(new TournamentEntrant
                {
                    TournamentId = tournament.Id,
                    PlayerId = candidate.Player.Id,
                    Player = candidate.Player
                });
            }
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
            _chipGameService.StartChipTournament(tournament, NewChipStartingChips);
        }
        else if (NewTournamentFormat == TournamentFormat.ModifiedSingleElimination)
        {
            // Round 1 is a random draw, not a rating seed - the generator does its own draw.
            _bracketService.GenerateModifiedSingleElimination(tournament);
        }
        else
        {
            // Only these formats actually seed by the chosen rating system - stamp it on the
            // tournament so the bracket/entrants displays know which rating to show alongside
            // each entrant (see SeedingService.GetRatingDisplay). Left null for Ring Game, Chip
            // Tournament, and Modified Single Elimination, whose "Seed by rating" control is
            // hidden/inapplicable.
            tournament.SeedingRatingSystem = NewTournamentRatingSystem;

            if (!useTeams)
            {
                missingRatingCount = tournament.Entrants.Count(e => !SeedingService.HasRating(e, NewTournamentRatingSystem));
            }
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
        NewEntryFee = 0m;
        NewHostFeePercentage = 0m;
        NewPayoutPlaceCount = 0;
        foreach (var candidate in EntrantCandidates)
        {
            candidate.IsSelected = false;
        }
        foreach (var candidate in TeamCandidates)
        {
            candidate.IsSelected = false;
        }

        await State.LoadTournamentsAsync();
        SelectedTournamentSummary = State.Tournaments.FirstOrDefault(t => t.Id == tournament.Id);
    }

    /// <summary>
    /// Permanently deletes the given tournament and all of its data. The caller (the Tournament
    /// tab) is responsible for confirming with the user first. Clears the active/selected
    /// tournament if it was the one removed, then refreshes the picker list.
    /// </summary>
    public async Task DeleteTournamentAsync(Tournament tournament)
    {
        var name = tournament.Name;
        var wasActive = State.ActiveTournament?.Id == tournament.Id;

        await _tournamentRepository.DeleteAsync(tournament.Id);

        if (wasActive)
        {
            // Tears down the bracket/tables/standings bound to the now-deleted tournament.
            await State.SelectTournamentAsync(null);
        }

        SelectedTournamentSummary = null;
        await State.LoadTournamentsAsync();
        StatusMessage = $"Deleted tournament '{name}'.";
    }

    [RelayCommand]
    private async Task StartMatchAsync(MatchRowViewModel? row)
    {
        if (row is null || State.ActiveTournament is null)
        {
            return;
        }

        if (!row.IsStartable)
        {
            return;
        }

        var tournament = State.ActiveTournament;
        var match = row.Match!; // IsStartable guarantees a materialized Match.

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

        if (row.Match is null)
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
                    ? RoundRobinStandingsService.ComputeStandings(tournament).FirstOrDefault()?.Entrant.DisplayName ?? "Unknown entrant"
                    : tournament.Entrants.FirstOrDefault(e => e.Id == match.WinnerEntrantId)?.DisplayName ?? "Unknown entrant";
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
