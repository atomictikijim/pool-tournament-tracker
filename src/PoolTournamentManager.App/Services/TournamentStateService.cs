using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PoolTournamentManager.App.ViewModels;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Interfaces;
using PoolTournamentManager.Core.Services;

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

    [ObservableProperty]
    private ObservableCollection<StandingsRowViewModel> _standings = new();

    [ObservableProperty]
    private ObservableCollection<RingSeatViewModel> _ringSeats = new();

    [ObservableProperty]
    private bool _isRingGame;

    /// <summary>One-line ring-game status, e.g. "Rack 3  ·  Pot $45  ·  Up: Alice".</summary>
    [ObservableProperty]
    private string _ringStatusLine = string.Empty;

    [ObservableProperty]
    private bool _isChipTournament;

    /// <summary>Every player's chip standing row (for the grid), leaders first.</summary>
    [ObservableProperty]
    private ObservableCollection<ChipStandingRowViewModel> _chipStandings = new();

    /// <summary>One-line chip-tournament status, e.g. "5 of 7 left  ·  3 chips each  ·  Pot $140".</summary>
    [ObservableProperty]
    private string _chipStatusLine = string.Empty;

    /// <summary>Each table's current occupants in the chip-tournament rotation.</summary>
    [ObservableProperty]
    private ObservableCollection<ChipTableBoardRowViewModel> _chipTableBoard = new();

    /// <summary>Entrants waiting to be seated next, in rotation order.</summary>
    [ObservableProperty]
    private ObservableCollection<ChipNextUpRowViewModel> _chipNextUp = new();

    /// <summary>True until table rotation has started - "Shuffle &amp; Seat Players" is only
    /// available up to that point (see ChipGameService.ShuffleAndSeatPlayers).</summary>
    [ObservableProperty]
    private bool _chipCanShuffle;

    /// <summary>Shown for every format except Ring Game once at least one payout place is
    /// configured - see PrizePayoutService.</summary>
    [ObservableProperty]
    private bool _showPrizePayouts;

    /// <summary>"Entry fees $200  ·  Host cut (10%) $20  ·  Prize pool $180".</summary>
    [ObservableProperty]
    private string _prizePoolSummaryLine = string.Empty;

    /// <summary>Per-entrant payout rows, sorted by finishing place. Empty for elimination
    /// brackets until the tournament completes.</summary>
    [ObservableProperty]
    private ObservableCollection<PrizePayoutRowViewModel> _prizePayouts = new();

    private readonly DispatcherTimer _matchTickTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    public TournamentStateService(ITournamentRepository tournamentRepository)
    {
        _tournamentRepository = tournamentRepository;
        _matchTickTimer.Tick += (_, _) => TickInProgressMatches();
        _matchTickTimer.Start();
    }

    /// <summary>Drives the live elapsed-time display on every in-progress match's card, once a
    /// second, for as long as the app is running.</summary>
    private void TickInProgressMatches()
    {
        foreach (var row in Rounds.SelectMany(r => r.Matches))
        {
            row.Tick();
        }
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
            Standings = new ObservableCollection<StandingsRowViewModel>();
            RingSeats = new ObservableCollection<RingSeatViewModel>();
            IsRingGame = false;
            RingStatusLine = string.Empty;
            return;
        }

        ActiveTournament = await _tournamentRepository.GetByIdAsync(tournamentId.Value);
        Tables = new ObservableCollection<Table>(ActiveTournament?.Tables ?? new List<Table>());
        RebuildRounds();
    }

    public void RebuildRounds()
    {
        var tournament = ActiveTournament;
        if (tournament is null)
        {
            Rounds = new ObservableCollection<RoundGroupViewModel>();
            Standings = new ObservableCollection<StandingsRowViewModel>();
            RingSeats = new ObservableCollection<RingSeatViewModel>();
            IsRingGame = false;
            RingStatusLine = string.Empty;
            ClearChipState();
            ClearPrizePayouts();
            return;
        }

        if (tournament.Format == TournamentFormat.RingGame)
        {
            RebuildRingGame(tournament);
            return;
        }

        if (tournament.Format == TournamentFormat.ChipTournament)
        {
            RebuildChipTournament(tournament);
            return;
        }

        IsRingGame = false;
        RingSeats = new ObservableCollection<RingSeatViewModel>();
        RingStatusLine = string.Empty;
        ClearChipState();

        if (tournament.Format == TournamentFormat.RoundRobin)
        {
            RebuildRoundRobinRounds(tournament);
            return;
        }

        Standings = new ObservableCollection<StandingsRowViewModel>();
        RebuildPrizePayouts(tournament);

        var rounds = new ObservableCollection<RoundGroupViewModel>();
        var bracket = tournament.Bracket;
        if (bracket is null || bracket.Nodes.Count == 0)
        {
            Rounds = rounds;
            return;
        }

        var sideRank = new Dictionary<BracketSide, int>
        {
            [BracketSide.Winners] = 0,
            [BracketSide.Losers] = 1,
            [BracketSide.GrandFinal] = 2,
            [BracketSide.Final] = 3
        };
        var groups = bracket.Nodes
            .GroupBy(n => (n.Side, n.RoundNumber))
            .OrderBy(g => sideRank[g.Key.Side])
            .ThenBy(g => g.Key.RoundNumber);

        // Every round's BracketNodes exist from tournament creation (see BracketGenerationService),
        // even for rounds whose Match hasn't materialized yet - so the whole bracket shape renders
        // immediately, with "TBD" placeholder rows for a node whose Match doesn't exist yet.
        // tournament.Entrants is always fully loaded (unlike Match.Player1Entrant/Player2Entrant -
        // see FinishMatchAsync's reload comment), so this lookup is reliable with no reload needed.
        var entrantsById = tournament.Entrants.ToDictionary(e => e.Id);

        foreach (var group in groups)
        {
            var matchRows = group
                .OrderBy(n => n.PositionInRound)
                .Select(n => n.Match is not null
                    ? new MatchRowViewModel(n.Match, tournament.SeedingRatingSystem)
                    : new MatchRowViewModel(
                        n.Slot1EntrantId is { } p1 ? entrantsById.GetValueOrDefault(p1) : null,
                        n.Slot2EntrantId is { } p2 ? entrantsById.GetValueOrDefault(p2) : null,
                        tournament.SeedingRatingSystem))
                .ToList();

            var title = BuildRoundTitle(bracket, group.Key.Side, group.Key.RoundNumber, group.Any(n => n.IsGrandFinalReset));
            rounds.Add(new RoundGroupViewModel(group.Key.RoundNumber, title, matchRows, group.Key.Side));
        }

        Rounds = rounds;
    }

    private void RebuildRingGame(Tournament tournament)
    {
        IsRingGame = true;
        ClearChipState();
        ClearPrizePayouts();
        Rounds = new ObservableCollection<RoundGroupViewModel>();
        Standings = new ObservableCollection<StandingsRowViewModel>();

        var detail = tournament.RingGame;
        var shooterId = detail?.CurrentShooterEntrantId;

        RingSeats = new ObservableCollection<RingSeatViewModel>(
            RingGameService.ComputeStandings(tournament)
                .OrderBy(r => r.RotationPosition)
                .Select(r => new RingSeatViewModel(r, shooterId)));

        if (detail is null)
        {
            RingStatusLine = string.Empty;
            return;
        }

        var pot = RingGameService.PotRemaining(tournament).ToString("C0");
        if (tournament.Status == TournamentStatus.Completed)
        {
            var leader = RingGameService.ComputeStandings(tournament).FirstOrDefault();
            RingStatusLine = $"Finished  ·  Pot {pot}  ·  Leader: {leader?.Entrant.Player?.FullName ?? "-"}";
        }
        else
        {
            var shooter = tournament.Entrants.FirstOrDefault(e => e.Id == shooterId)?.Player?.FullName ?? "-";
            RingStatusLine = $"Rack {detail.CurrentRackNumber}  ·  Pot {pot}  ·  Up: {shooter}";
        }
    }

    private void RebuildChipTournament(Tournament tournament)
    {
        IsRingGame = false;
        RingSeats = new ObservableCollection<RingSeatViewModel>();
        RingStatusLine = string.Empty;
        Rounds = new ObservableCollection<RoundGroupViewModel>();
        Standings = new ObservableCollection<StandingsRowViewModel>();

        IsChipTournament = true;

        var rows = ChipGameService.ComputeStandings(tournament);
        ChipStandings = new ObservableCollection<ChipStandingRowViewModel>(rows.Select(r => new ChipStandingRowViewModel(r)));

        var board = ChipGameService.ComputeTableBoard(tournament);
        ChipTableBoard = new ObservableCollection<ChipTableBoardRowViewModel>(
            board.Tables.Select(s => new ChipTableBoardRowViewModel(s)));
        ChipNextUp = new ObservableCollection<ChipNextUpRowViewModel>(
            board.NextUp.Select((e, i) => new ChipNextUpRowViewModel(i + 1, e)));
        ChipCanShuffle = tournament.ChipGame?.Entries.All(e => e.TableId is null) ?? true;

        var pot = PrizePayoutService.TotalEntryFees(tournament).ToString("C0");
        var total = tournament.Entrants.Count;
        var active = rows.Count(r => !r.IsEliminated);

        if (tournament.Status == TournamentStatus.Completed)
        {
            var champion = rows.FirstOrDefault(r => r.Place == 1)?.Entrant.Player?.FullName ?? "-";
            ChipStatusLine = $"Finished  ·  Pot {pot}  ·  Winner: {champion}";
        }
        else
        {
            var chips = tournament.ChipGame?.StartingChips ?? 0;
            ChipStatusLine = $"{active} of {total} left  ·  {chips} chips each  ·  Pot {pot}";
        }

        RebuildPrizePayouts(tournament);
    }

    private void ClearChipState()
    {
        IsChipTournament = false;
        ChipStandings = new ObservableCollection<ChipStandingRowViewModel>();
        ChipStatusLine = string.Empty;
        ChipTableBoard = new ObservableCollection<ChipTableBoardRowViewModel>();
        ChipNextUp = new ObservableCollection<ChipNextUpRowViewModel>();
        ChipCanShuffle = false;
    }

    private void ClearPrizePayouts()
    {
        ShowPrizePayouts = false;
        PrizePoolSummaryLine = string.Empty;
        PrizePayouts = new ObservableCollection<PrizePayoutRowViewModel>();
    }

    /// <summary>
    /// Rebuilds the shared "Prize Payouts" panel (Tournament tab and Display window) for any
    /// format except Ring Game, which has its own separate money model. Shows the entry-fee
    /// totals as soon as a payout is configured, even before placements are known; per-entrant
    /// payout rows populate once PrizePayoutService can determine them (immediately for Round
    /// Robin/Chip Tournament, only once completed for elimination brackets).
    /// </summary>
    private void RebuildPrizePayouts(Tournament tournament)
    {
        ShowPrizePayouts = tournament.Format != TournamentFormat.RingGame && tournament.PrizePlaces.Count > 0;
        if (!ShowPrizePayouts)
        {
            PrizePoolSummaryLine = string.Empty;
            PrizePayouts = new ObservableCollection<PrizePayoutRowViewModel>();
            return;
        }

        var totalFees = PrizePayoutService.TotalEntryFees(tournament).ToString("C0");
        var pool = PrizePayoutService.PrizePool(tournament).ToString("C0");
        PrizePoolSummaryLine = tournament.HostFeePercentage > 0
            ? $"Entry fees {totalFees}  ·  Host cut ({tournament.HostFeePercentage:0.##}%) {PrizePayoutService.HostCut(tournament):C0}  ·  Prize pool {pool}"
            : $"Entry fees {totalFees}  ·  Prize pool {pool}";

        PrizePayouts = new ObservableCollection<PrizePayoutRowViewModel>(
            PrizePayoutService.ComputePayouts(tournament)
                .OrderBy(r => r.PlaceRangeStart)
                .Select(r => new PrizePayoutRowViewModel(r)));
    }

    private void RebuildRoundRobinRounds(Tournament tournament)
    {
        var rounds = new ObservableCollection<RoundGroupViewModel>();
        var groups = tournament.Matches
            .Where(m => m.RoundNumber is not null)
            .GroupBy(m => m.RoundNumber!.Value)
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            var matchRows = group.Select(m => new MatchRowViewModel(m, tournament.SeedingRatingSystem)).ToList();
            rounds.Add(new RoundGroupViewModel(group.Key, $"Round {group.Key}", matchRows));
        }

        Rounds = rounds;
        Standings = new ObservableCollection<StandingsRowViewModel>(
            RoundRobinStandingsService.ComputeStandings(tournament)
                .Select((row, index) => new StandingsRowViewModel(index + 1, row)));
        RebuildPrizePayouts(tournament);
    }

    private static string BuildRoundTitle(BracketDetail bracket, BracketSide side, int roundNumber, bool isReset)
    {
        if (side == BracketSide.GrandFinal)
        {
            return isReset ? "Bracket Reset" : "Grand Final";
        }

        var maxRoundForSide = bracket.Nodes.Where(n => n.Side == side).Max(n => n.RoundNumber);

        if (bracket.Kind == BracketKind.ModifiedSingleElimination)
        {
            if (side == BracketSide.Final)
            {
                if (roundNumber == maxRoundForSide) return "Final";
                if (roundNumber == maxRoundForSide - 1) return "Semifinals";
                return $"Round {roundNumber}";
            }

            var podPrefix = side == BracketSide.Winners ? "Winners" : "Losers";
            return $"{podPrefix} Round {roundNumber}";
        }

        if (bracket.Kind == BracketKind.SingleElimination)
        {
            if (roundNumber == maxRoundForSide) return "Final";
            if (roundNumber == maxRoundForSide - 1) return "Semifinals";
            return $"Round {roundNumber}";
        }

        var prefix = side == BracketSide.Winners ? "WB" : "LB";
        return roundNumber == maxRoundForSide ? $"{prefix} Final" : $"{prefix} Round {roundNumber}";
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
