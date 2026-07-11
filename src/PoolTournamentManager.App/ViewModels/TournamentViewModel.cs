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

    // Options for the Tournament tab's status filter. A bracket/round-robin tournament sits at
    // NotStarted until its first match is actually started (see StartMatchAsync), then InProgress
    // until it finishes - Ring Game/Chip Tournament skip straight to InProgress on creation, since
    // they have no separate "not started" bracket/schedule phase.
    public const string StatusFilterAll = "All";
    public const string StatusFilterNotStarted = "Not Started";
    public const string StatusFilterInProgress = "In Progress";
    public const string StatusFilterCompleted = "Completed";

    /// <summary>Choices for the tournament-list status filter on the Tournament tab.</summary>
    public ObservableCollection<string> AvailableStatusFilters { get; } =
        new() { StatusFilterAll, StatusFilterNotStarted, StatusFilterInProgress, StatusFilterCompleted };

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

    /// <summary>True while the active tournament is still NotStarted and is a format with a
    /// seeded bracket/schedule (not Ring Game/Chip Tournament) - gates the "Reshuffle Bracket"
    /// button, since once a match starts the field is locked in for good.</summary>
    public bool CanReshuffleBracket { get; private set; }

    /// <summary>Fired after a tournament is created or its settings are saved, so the app can
    /// switch to the Tournament tab and show it - see MainWindowViewModel.</summary>
    public event Action? TournamentReady;

    /// <summary>The tournament currently being edited via "Edit Tournament" (null while the form
    /// is in plain create mode) - see BeginEditTournament/SaveTournamentSettingsAsync.</summary>
    private Tournament? _editingTournament;

    /// <summary>True while the Tournament Settings form is editing an existing tournament rather
    /// than building a new one - swaps "Create Tournament" for "Save Settings".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCreatingNewTournament))]
    [NotifyPropertyChangedFor(nameof(FormHeaderText))]
    private bool _isEditingExistingTournament;

    public bool IsCreatingNewTournament => !IsEditingExistingTournament;

    public string FormHeaderText => IsEditingExistingTournament ? "Edit Tournament" : "Create Tournament";

    /// <summary>True while the tournament selected in the Tournament tab's list is NotStarted,
    /// so its settings can still be safely edited/rebuilt - gates the "Edit Tournament" button.</summary>
    public bool CanEditSelectedTournament => SelectedTournamentSummary?.Status == TournamentStatus.NotStarted;

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

    /// <summary>When true, a Chip Tournament assigns starting chips by skill range
    /// (<see cref="NewChipRatingSystem"/> + <see cref="NewChipRanges"/>) instead of the flat
    /// <see cref="NewChipStartingChips"/>, which then becomes the fallback for unrated players.</summary>
    [ObservableProperty]
    private bool _newChipUsesSkillRanges;

    /// <summary>Which rating drives the chip skill ranges (when <see cref="NewChipUsesSkillRanges"/>).</summary>
    [ObservableProperty]
    private RatingSystem _newChipRatingSystem = RatingSystem.Fargo;

    /// <summary>The editable "skill range → chips" rows shown when <see cref="NewChipUsesSkillRanges"/>.</summary>
    public ObservableCollection<ChipRangeInputViewModel> NewChipRanges { get; } = new();

    /// <summary>Seed a sensible starter set of ranges the first time skill-based chips are turned on
    /// (the operator edits them from there), so the grid is never empty and confusing.</summary>
    partial void OnNewChipUsesSkillRangesChanged(bool value)
    {
        if (value && NewChipRanges.Count == 0)
        {
            NewChipRanges.Add(new ChipRangeInputViewModel { MinRating = 650, MaxRating = null, Chips = 3 });
            NewChipRanges.Add(new ChipRangeInputViewModel { MinRating = 550, MaxRating = 649, Chips = 4 });
            NewChipRanges.Add(new ChipRangeInputViewModel { MinRating = 450, MaxRating = 549, Chips = 5 });
            NewChipRanges.Add(new ChipRangeInputViewModel { MinRating = null, MaxRating = 449, Chips = 6 });
        }
    }

    [RelayCommand]
    private void AddChipRange() => NewChipRanges.Add(new ChipRangeInputViewModel());

    [RelayCommand]
    private void RemoveChipRange(ChipRangeInputViewModel? row)
    {
        if (row is not null)
        {
            NewChipRanges.Remove(row);
        }
    }

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
            StatusFilterNotStarted => tournament.Status == TournamentStatus.NotStarted,
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

    private const double MinBracketZoom = 0.15;
    private const double MaxBracketZoom = 2.0;
    private const double BracketZoomStep = 0.1;

    /// <summary>Scale factor applied to the bracket tree via a LayoutTransform - lets the operator
    /// zoom in for readability or out to see the whole tree at once. 1.0 = actual size.</summary>
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
        // Picking a different tournament while mid-edit would otherwise leave the form showing
        // one tournament's settings while "Save Settings" quietly writes into another.
        if (IsEditingExistingTournament && _editingTournament?.Id != value?.Id)
        {
            ResetTournamentForm();
        }

        OnPropertyChanged(nameof(CanEditSelectedTournament));
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
            RefreshTournamentLifecycleFlags();
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

    /// <summary>Recomputes every derived flag that depends on the active tournament's current
    /// lifecycle state (CanAddEntrant, CanReshuffleBracket, CanEditSelectedTournament) - call
    /// this anywhere that state could change: selecting a tournament, adding an entrant, or
    /// starting a match.</summary>
    private void RefreshTournamentLifecycleFlags()
    {
        var tournament = State.ActiveTournament;

        CanAddEntrant = ComputeCanAddEntrant(tournament);
        OnPropertyChanged(nameof(CanAddEntrant));

        CanReshuffleBracket = tournament is not null
            && tournament.Status == TournamentStatus.NotStarted
            && tournament.Format is TournamentFormat.SingleElimination or TournamentFormat.DoubleElimination
                or TournamentFormat.ModifiedSingleElimination or TournamentFormat.RoundRobin;
        OnPropertyChanged(nameof(CanReshuffleBracket));

        OnPropertyChanged(nameof(CanEditSelectedTournament));
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
        // Double elimination accepts any count >= 2: the bracket plays the largest power of two that
        // fits and the overflow (lowest seeds) is waitlisted until the field reaches the next power
        // of two (see BracketGenerationService.GenerateDoubleElimination). No count is rejected here.

        if (tournament.Format == TournamentFormat.ModifiedSingleElimination && !BracketGenerationService.IsValidModifiedSingleEliminationCount(newTotal))
        {
            StatusMessage = "Modified Single Elimination needs entrants that split into brackets of 6-8 (6-8, 12-16, 18-24, ...) - 9-11 and 17 can't fill a second bracket to 6.";
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
            RefreshTournamentLifecycleFlags();
            SelectedPlayerToAdd = null;
            SelectedTeamToAdd = null;
            StatusMessage = $"Added {addedName}.{DoubleEliminationWaitlistSuffix(tournament)}";
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void RegenerateRoundRobin(Tournament tournament, bool forceRandomSeed = false)
    {
        foreach (var oldMatch in tournament.Matches.ToList())
        {
            _tournamentRepository.TrackRemoved(oldMatch);
        }
        tournament.Matches.Clear();

        if (forceRandomSeed)
        {
            SeedingService.RandomDraw(tournament.Entrants);
        }
        else
        {
            SeedingService.AssignSeeds(tournament.Entrants, tournament.SeedingRatingSystem ?? RatingSystem.Fargo);
        }
        _roundRobinService.GenerateSchedule(tournament);

        foreach (var newMatch in tournament.Matches)
        {
            _tournamentRepository.TrackNew(newMatch);
        }
    }

    /// <summary>A trailing " N playing, M waitlisted ..." note for a double-elimination tournament
    /// whose field isn't yet a power of two, or empty otherwise (any other format, or a field that
    /// exactly fills the bracket). Appended to the "Added ..." status so the director sees at a
    /// glance who's in the bracket and how many more entrants would admit the whole waitlist.</summary>
    private static string DoubleEliminationWaitlistSuffix(Tournament tournament)
    {
        if (tournament.Format != TournamentFormat.DoubleElimination)
        {
            return string.Empty;
        }

        var count = tournament.Entrants.Count;
        var waitlisted = BracketGenerationService.DoubleEliminationWaitlistCount(count);
        if (waitlisted == 0)
        {
            return string.Empty;
        }

        var playing = BracketGenerationService.DoubleEliminationBracketSize(count);
        var nextSize = playing * 2;
        var needed = nextSize - count;
        return $" {playing} playing, {waitlisted} on the waitlist - add {needed} more to fill a {nextSize}-player bracket.";
    }

    private void RegenerateBracket(Tournament tournament, Func<Tournament, BracketDetail> generate, bool forceRandomSeed = false)
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

        if (forceRandomSeed)
        {
            SeedingService.RandomDraw(tournament.Entrants);
        }
        else
        {
            SeedingService.AssignSeeds(tournament.Entrants, tournament.SeedingRatingSystem ?? RatingSystem.Fargo);
        }
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

    /// <summary>
    /// Regenerates the active tournament's bracket/schedule from a fresh 100% random shuffle of
    /// its existing entrants, ignoring the tournament's configured seeding entirely - only
    /// available while the tournament is NotStarted (see CanReshuffleBracket); once a match has
    /// actually started, the field is locked in for good.
    /// </summary>
    [RelayCommand]
    private async Task ReshuffleBracketAsync()
    {
        var tournament = State.ActiveTournament;
        if (tournament is null || tournament.Status != TournamentStatus.NotStarted)
        {
            return;
        }

        switch (tournament.Format)
        {
            case TournamentFormat.RoundRobin:
                RegenerateRoundRobin(tournament, forceRandomSeed: true);
                break;
            case TournamentFormat.SingleElimination:
                RegenerateBracket(tournament, _bracketService.GenerateSingleElimination, forceRandomSeed: true);
                break;
            case TournamentFormat.DoubleElimination:
                RegenerateBracket(tournament, _bracketService.GenerateDoubleElimination, forceRandomSeed: true);
                break;
            case TournamentFormat.ModifiedSingleElimination:
                RegenerateBracket(tournament, _bracketService.GenerateModifiedSingleElimination, forceRandomSeed: true);
                break;
            default:
                return;
        }

        await _tournamentRepository.SaveChangesAsync();
        await State.SelectTournamentAsync(tournament.Id);
        StatusMessage = tournament.Format == TournamentFormat.RoundRobin ? "Schedule reshuffled." : "Bracket reshuffled.";
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
        var useTeams = UseTeams && IsTeamEligibleFormat;
        var selectedPlayers = EntrantCandidates.Where(c => c.IsSelected).ToList();
        var selectedTeams = TeamCandidates.Where(c => c.IsSelected).ToList();
        var entrantCount = useTeams ? selectedTeams.Count : selectedPlayers.Count;

        if (!ValidateTournamentForm(useTeams, entrantCount))
        {
            return;
        }

        var tournament = new Tournament { Name = NewTournamentName };
        var missingRatingCount = PopulateTournamentContent(tournament, useTeams, selectedPlayers, selectedTeams);

        await _tournamentRepository.AddAsync(tournament);

        StatusMessage = missingRatingCount > 0
            ? $"Created '{tournament.Name}' with {tournament.Entrants.Count} entrants ({missingRatingCount} missing a {NewTournamentRatingSystem} rating, seeded last)."
            : $"Created '{tournament.Name}' with {tournament.Entrants.Count} entrants.";

        await FinishCreateOrSaveAsync(tournament);
    }

    /// <summary>
    /// Populates the Tournament Settings form from an existing NotStarted tournament so it can be
    /// edited in place, and switches the form into "Save Settings" mode - called by the Tournament
    /// tab's "Edit Tournament" button (see CanEditSelectedTournament).
    /// </summary>
    public void BeginEditTournament(Tournament tournament)
    {
        _editingTournament = tournament;
        IsEditingExistingTournament = true;

        NewTournamentName = tournament.Name;
        NewTournamentGameType = tournament.GameType;
        NewTournamentFormat = tournament.Format;
        UseTeams = tournament.UsesTeams;
        NewTournamentRatingSystem = tournament.SeedingRatingSystem ?? RatingSystem.Fargo;
        NewTournamentTableCount = tournament.Tables.Count;
        NewEntryFee = tournament.EntryFee;
        NewHostFeePercentage = tournament.HostFeePercentage;

        NewPayoutPlaceCount = tournament.PrizePlaces.Count;
        foreach (var place in tournament.PrizePlaces)
        {
            var input = NewPrizePlaceInputs.FirstOrDefault(p => p.Place == place.Place);
            if (input is not null)
            {
                input.Percentage = place.Percentage;
            }
        }

        if (tournament.RingGame is not null)
        {
            NewRingBuyIn = tournament.RingGame.BuyInAmount;
            NewRingFivePayout = tournament.RingGame.FiveBallPayout;
            NewRingNinePayout = tournament.RingGame.NineBallPayout;
        }
        if (tournament.ChipGame is not null)
        {
            NewChipStartingChips = tournament.ChipGame.StartingChips;
            NewChipUsesSkillRanges = tournament.ChipGame.ChipRatingSystem is not null && tournament.ChipGame.StartingRules.Count > 0;
            NewChipRatingSystem = tournament.ChipGame.ChipRatingSystem ?? RatingSystem.Fargo;
            NewChipRanges.Clear();
            foreach (var rule in tournament.ChipGame.StartingRules.OrderBy(r => r.Sequence))
            {
                NewChipRanges.Add(new ChipRangeInputViewModel { MinRating = rule.MinRating, MaxRating = rule.MaxRating, Chips = rule.Chips });
            }
        }

        var playerIds = tournament.Entrants.Where(e => e.PlayerId is not null).Select(e => e.PlayerId!.Value).ToHashSet();
        var teamIds = tournament.Entrants.Where(e => e.TeamId is not null).Select(e => e.TeamId!.Value).ToHashSet();
        foreach (var candidate in EntrantCandidates)
        {
            candidate.IsSelected = playerIds.Contains(candidate.Player.Id);
        }
        foreach (var candidate in TeamCandidates)
        {
            candidate.IsSelected = teamIds.Contains(candidate.Team.Id);
        }

        StatusMessage = $"Editing '{tournament.Name}'.";
    }

    /// <summary>
    /// Saves the Tournament Settings form back onto the tournament currently being edited (see
    /// BeginEditTournament) - wipes its existing entrants/tables/bracket/schedule/prize places/
    /// ring or chip detail and rebuilds all of it fresh from the form, exactly like creating a
    /// tournament from scratch, but reusing the same tournament record/Id.
    /// </summary>
    [RelayCommand]
    private async Task SaveTournamentSettingsAsync()
    {
        var tournament = _editingTournament;
        if (tournament is null)
        {
            return;
        }

        var useTeams = UseTeams && IsTeamEligibleFormat;
        var selectedPlayers = EntrantCandidates.Where(c => c.IsSelected).ToList();
        var selectedTeams = TeamCandidates.Where(c => c.IsSelected).ToList();
        var entrantCount = useTeams ? selectedTeams.Count : selectedPlayers.Count;

        if (!ValidateTournamentForm(useTeams, entrantCount))
        {
            return;
        }

        ClearTournamentContent(tournament);
        var missingRatingCount = PopulateTournamentContent(tournament, useTeams, selectedPlayers, selectedTeams);

        await _tournamentRepository.SaveChangesAsync();

        StatusMessage = missingRatingCount > 0
            ? $"Saved '{tournament.Name}' with {tournament.Entrants.Count} entrants ({missingRatingCount} missing a {NewTournamentRatingSystem} rating, seeded last)."
            : $"Saved '{tournament.Name}' with {tournament.Entrants.Count} entrants.";

        await FinishCreateOrSaveAsync(tournament);
    }

    /// <summary>Validates the Tournament Settings form, setting StatusMessage and returning false
    /// on the first failure - shared by both CreateTournamentAsync and SaveTournamentSettingsAsync
    /// so the two stay in lockstep.</summary>
    private bool ValidateTournamentForm(bool useTeams, int entrantCount)
    {
        if (string.IsNullOrWhiteSpace(NewTournamentName))
        {
            StatusMessage = "Enter a tournament name.";
            return false;
        }

        if (entrantCount < 2)
        {
            StatusMessage = useTeams ? "Select at least 2 teams." : "Select at least 2 players.";
            return false;
        }

        // Double Elimination accepts any count >= 2: the bracket runs the largest power of two that
        // fits and waitlists the overflow (see BracketGenerationService.GenerateDoubleElimination).

        if (NewTournamentFormat == TournamentFormat.ModifiedSingleElimination && !BracketGenerationService.IsValidModifiedSingleEliminationCount(entrantCount))
        {
            StatusMessage = "Modified Single Elimination needs entrants that split into brackets of 6-8 (6-8, 12-16, 18-24, ...).";
            return false;
        }

        if (NewTournamentFormat != TournamentFormat.RingGame && NewTournamentTableCount < 1)
        {
            StatusMessage = "Enter the number of available tables.";
            return false;
        }

        if (IsCreatingChipTournament)
        {
            if (NewChipStartingChips < 1)
            {
                StatusMessage = "Starting chips must be at least 1.";
                return false;
            }
            if (NewChipUsesSkillRanges)
            {
                if (NewChipRanges.Count == 0)
                {
                    StatusMessage = "Add at least one chip range, or turn off skill-based chips.";
                    return false;
                }
                foreach (var range in NewChipRanges)
                {
                    if (range.Chips < 1)
                    {
                        StatusMessage = "Each chip range needs at least 1 chip.";
                        return false;
                    }
                    if (range.MinRating is int min && range.MaxRating is int max && min > max)
                    {
                        StatusMessage = "A chip range's minimum can't be greater than its maximum.";
                        return false;
                    }
                }
            }
        }

        if (ShowEntryFeeSection)
        {
            if (NewEntryFee < 0)
            {
                StatusMessage = "Entry fee can't be negative.";
                return false;
            }
            if (NewHostFeePercentage < 0 || NewHostFeePercentage > 100)
            {
                StatusMessage = "Host fee percentage must be between 0 and 100.";
                return false;
            }
            if (!IsPrizePlacePercentageValid)
            {
                StatusMessage = $"Prize place percentages must add up to 100% (currently {PrizePlacePercentageTotal:0.##}%).";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Builds a tournament's full owned content (entrants, tables, prize places, and the
    /// format-specific bracket/schedule/ring/chip setup) from the create/edit form fields, onto a
    /// Tournament whose Id is already set - either a brand-new one about to be AddAsync'd (which
    /// cascades every reachable new entity to Added automatically), or an existing tracked one
    /// whose previous content was just cleared (see ClearTournamentContent). Either way every new
    /// child gets an explicit TrackNew: for the existing-tournament case the parent is already
    /// tracked (Unchanged), so EF can't infer that a child added to its navigation collection is
    /// new - and doing the same for the brand-new case is harmless, since these child entities
    /// only carry a plain Guid TournamentId (no Tournament navigation property), so marking them
    /// Added early never accidentally graph-walks into (and re-adds) the parent.
    /// </summary>
    private int PopulateTournamentContent(Tournament tournament, bool useTeams, List<PlayerSelectionItem> selectedPlayers, List<TeamSelectionItem> selectedTeams)
    {
        tournament.Name = NewTournamentName;
        tournament.GameType = NewTournamentGameType;
        tournament.Format = NewTournamentFormat;
        tournament.UsesTeams = useTeams;
        tournament.SeedingRatingSystem = null;

        if (ShowEntryFeeSection)
        {
            tournament.EntryFee = NewEntryFee;
            tournament.HostFeePercentage = NewHostFeePercentage;
            foreach (var place in NewPrizePlaceInputs)
            {
                var prizePlace = new TournamentPrizePlace { TournamentId = tournament.Id, Place = place.Place, Percentage = place.Percentage };
                tournament.PrizePlaces.Add(prizePlace);
                _tournamentRepository.TrackNew(prizePlace);
            }
        }
        else
        {
            tournament.EntryFee = 0m;
            tournament.HostFeePercentage = 0m;
        }

        if (useTeams)
        {
            foreach (var candidate in selectedTeams)
            {
                var entrant = new TournamentEntrant { TournamentId = tournament.Id, TeamId = candidate.Team.Id, Team = candidate.Team };
                tournament.Entrants.Add(entrant);
                _tournamentRepository.TrackNew(entrant);
            }
        }
        else
        {
            foreach (var candidate in selectedPlayers)
            {
                var entrant = new TournamentEntrant { TournamentId = tournament.Id, PlayerId = candidate.Player.Id, Player = candidate.Player };
                tournament.Entrants.Add(entrant);
                _tournamentRepository.TrackNew(entrant);
            }
        }

        if (NewTournamentFormat != TournamentFormat.RingGame)
        {
            for (var i = 1; i <= NewTournamentTableCount; i++)
            {
                var table = new Table { TournamentId = tournament.Id, Label = $"Table {i}" };
                tournament.Tables.Add(table);
                _tournamentRepository.TrackNew(table);
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
            // Ad-hoc "loser loses a life" play; no seeding or pairings. Starting chips are either
            // flat or, when skill ranges are on, per-player by rating - snapshotted onto each
            // entrant inside StartChipTournament.
            var rules = NewChipUsesSkillRanges
                ? NewChipRanges.Select(r => new ChipStartingRule { MinRating = r.MinRating, MaxRating = r.MaxRating, Chips = r.Chips }).ToList()
                : new List<ChipStartingRule>();
            _chipGameService.StartChipTournament(
                tournament,
                NewChipStartingChips,
                NewChipUsesSkillRanges ? NewChipRatingSystem : null,
                rules);
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

        if (tournament.Bracket is not null)
        {
            _tournamentRepository.TrackNew(tournament.Bracket);
            foreach (var node in tournament.Bracket.Nodes)
            {
                _tournamentRepository.TrackNew(node);
            }
        }
        foreach (var match in tournament.Matches)
        {
            _tournamentRepository.TrackNew(match);
        }
        if (tournament.RingGame is not null)
        {
            _tournamentRepository.TrackNew(tournament.RingGame);
            foreach (var entry in tournament.RingGame.LedgerEntries)
            {
                _tournamentRepository.TrackNew(entry);
            }
        }
        if (tournament.ChipGame is not null)
        {
            _tournamentRepository.TrackNew(tournament.ChipGame);
            foreach (var rule in tournament.ChipGame.StartingRules)
            {
                _tournamentRepository.TrackNew(rule);
            }
            foreach (var entry in tournament.ChipGame.Entries)
            {
                _tournamentRepository.TrackNew(entry);
            }
        }

        return missingRatingCount;
    }

    /// <summary>Wipes every owned child of a tournament (entrants, tables, matches, prize places,
    /// bracket+nodes, ring/chip detail+entries) so PopulateTournamentContent can rebuild it from
    /// scratch - the edit-in-place counterpart of a fresh Tournament having no children yet.</summary>
    private void ClearTournamentContent(Tournament tournament)
    {
        // Matches/ledger/chip entries hold a required (non-nullable) FK to TournamentEntrant -
        // clearing Entrants first would sever that relationship while EF still sees the
        // dependent row as live, which throws ("association... has been severed") even though
        // the dependent is *also* about to be deleted in this same batch. Removing every
        // dependent before touching Entrants avoids that entirely.
        if (tournament.Bracket is not null)
        {
            foreach (var node in tournament.Bracket.Nodes.ToList())
            {
                _tournamentRepository.TrackRemoved(node);
            }
            _tournamentRepository.TrackRemoved(tournament.Bracket);
            tournament.Bracket = null;
        }

        foreach (var match in tournament.Matches.ToList())
        {
            _tournamentRepository.TrackRemoved(match);
        }
        tournament.Matches.Clear();

        if (tournament.RingGame is not null)
        {
            foreach (var entry in tournament.RingGame.LedgerEntries.ToList())
            {
                _tournamentRepository.TrackRemoved(entry);
            }
            _tournamentRepository.TrackRemoved(tournament.RingGame);
            tournament.RingGame = null;
        }

        if (tournament.ChipGame is not null)
        {
            foreach (var entry in tournament.ChipGame.Entries.ToList())
            {
                _tournamentRepository.TrackRemoved(entry);
            }
            foreach (var rule in tournament.ChipGame.StartingRules.ToList())
            {
                _tournamentRepository.TrackRemoved(rule);
            }
            _tournamentRepository.TrackRemoved(tournament.ChipGame);
            tournament.ChipGame = null;
        }

        // Safe to clear now that every dependent referencing an Entrant is gone.
        foreach (var entrant in tournament.Entrants.ToList())
        {
            _tournamentRepository.TrackRemoved(entrant);
        }
        tournament.Entrants.Clear();

        foreach (var table in tournament.Tables.ToList())
        {
            _tournamentRepository.TrackRemoved(table);
        }
        tournament.Tables.Clear();

        foreach (var place in tournament.PrizePlaces.ToList())
        {
            _tournamentRepository.TrackRemoved(place);
        }
        tournament.PrizePlaces.Clear();
    }

    /// <summary>Clears the create/edit form back to its blank-create-form default, including
    /// dropping out of edit mode.</summary>
    private void ResetTournamentForm()
    {
        NewTournamentName = string.Empty;
        NewEntryFee = 0m;
        NewHostFeePercentage = 0m;
        NewPayoutPlaceCount = 0;
        NewChipUsesSkillRanges = false;
        NewChipRanges.Clear();
        foreach (var candidate in EntrantCandidates)
        {
            candidate.IsSelected = false;
        }
        foreach (var candidate in TeamCandidates)
        {
            candidate.IsSelected = false;
        }
        _editingTournament = null;
        IsEditingExistingTournament = false;
    }

    /// <summary>Shared tail of both CreateTournamentAsync and SaveTournamentSettingsAsync: resets
    /// the form, reloads the tournament list, re-selects the tournament just built (which opens it
    /// on the Tournament tab), and asks the app to switch to that tab.</summary>
    private async Task FinishCreateOrSaveAsync(Tournament tournament)
    {
        ResetTournamentForm();
        await State.LoadTournamentsAsync();
        SelectedTournamentSummary = State.Tournaments.FirstOrDefault(t => t.Id == tournament.Id);
        TournamentReady?.Invoke();
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

        // The tournament sits at NotStarted (see BracketGenerationService/RoundRobinSchedulingService)
        // until its very first match actually starts - that's also the point past which reshuffling
        // the bracket and editing the tournament's settings are no longer allowed.
        if (tournament.Status == TournamentStatus.NotStarted)
        {
            tournament.Status = TournamentStatus.InProgress;
        }

        match.Status = MatchStatus.InProgress;
        match.StartedAtUtc = DateTime.UtcNow;
        // Persists match.TableId along with the status change - the table picker no longer
        // needs its own explicit "Save Table Assignments" step.
        await _tournamentRepository.SaveChangesAsync();
        State.RebuildRounds();
        RefreshTournamentLifecycleFlags();
        await RefreshTournamentSummaryAsync();
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

            // Fix up navigation properties on newly-materialized matches in-memory instead of
            // reloading the whole tournament: RecordMatchResult only sets
            // Player1EntrantId/Player2EntrantId on a new Match, not the Player1Entrant/
            // Player2Entrant navigation MatchRowViewModel reads names from. tournament.Entrants
            // is always fully loaded (see TournamentStateService.RebuildRounds), so this is a
            // cheap in-memory lookup - no need for GetByIdAsync's six-way Include() reload, which
            // multiplies out into a huge duplicated-row result set and blocks the UI thread.
            var entrantsById = tournament.Entrants.ToDictionary(e => e.Id);
            foreach (var newMatch in newMatches)
            {
                newMatch.Player1Entrant = entrantsById.GetValueOrDefault(newMatch.Player1EntrantId);
                if (newMatch.Player2EntrantId is { } player2EntrantId)
                {
                    newMatch.Player2Entrant = entrantsById.GetValueOrDefault(player2EntrantId);
                }
            }

            State.RebuildRounds();
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
        State.RebuildRounds();
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
    private async Task RecordChipTableGameAsync(ChipGameOutcome? outcome)
    {
        var tournament = State.ActiveTournament;
        if (outcome is null || tournament?.ChipGame is null)
        {
            return;
        }

        try
        {
            var loserName = tournament.Entrants.FirstOrDefault(e => e.Id == outcome.LoserId)?.Player?.FullName ?? "Player";
            var entry = _chipGameService.RecordGame(tournament, outcome.TableId, outcome.WinnerId, outcome.LoserId);
            _tournamentRepository.TrackNew(entry);
            await _tournamentRepository.SaveChangesAsync();

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
            // Most likely a stale board (seating changed between render and click) - refresh so
            // the buttons the operator sees next reflect reality instead of failing again.
            State.RebuildRounds();
            StatusMessage = ex.Message;
        }
    }

    /// <summary>Tournament-director action: add or remove a chip from one player mid-tournament (a
    /// penalty or a bought chip). Persists the per-entrant adjustment and rebuilds the board/
    /// standings; the Core service enforces that a player can't be taken below zero.</summary>
    [RelayCommand]
    private async Task AdjustChipsAsync(ChipAdjustmentRequest? request)
    {
        var tournament = State.ActiveTournament;
        if (request is null || tournament?.ChipGame is null)
        {
            return;
        }

        try
        {
            var name = tournament.Entrants.FirstOrDefault(e => e.Id == request.EntrantId)?.Player?.FullName ?? "Player";
            _chipGameService.AdjustChips(tournament, request.EntrantId, request.Delta);
            await _tournamentRepository.SaveChangesAsync();

            State.RebuildRounds();
            await RefreshTournamentSummaryAsync();

            if (tournament.Status == TournamentStatus.Completed)
            {
                var champion = ChipGameService.ComputeStandings(tournament).FirstOrDefault(r => r.Place == 1)?.Entrant.Player?.FullName ?? "Unknown player";
                StatusMessage = $"Chip tournament over - {champion} wins!";
            }
            else
            {
                var count = Math.Abs(request.Delta);
                StatusMessage = request.Delta > 0
                    ? $"Added {count} chip{(count == 1 ? "" : "s")} to {name}."
                    : $"Removed {count} chip{(count == 1 ? "" : "s")} from {name}.";
            }
        }
        catch (InvalidOperationException ex)
        {
            State.RebuildRounds();
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ShuffleAndSeatPlayersAsync()
    {
        var tournament = State.ActiveTournament;
        if (tournament?.ChipGame is null)
        {
            return;
        }

        try
        {
            _chipGameService.ShuffleAndSeatPlayers(tournament);
            await _tournamentRepository.SaveChangesAsync();
            State.RebuildRounds();
            StatusMessage = "Players shuffled and seated at tables.";
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
