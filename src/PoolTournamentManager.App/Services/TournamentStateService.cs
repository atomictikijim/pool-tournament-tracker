using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
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

    /// <summary>Names of entrants currently on the double-elimination waitlist (the overflow past the
    /// largest power of two that fits), lowest seed first. Empty for every other format and for a
    /// double-elimination field that exactly fills its bracket. See
    /// BracketGenerationService.GenerateDoubleElimination.</summary>
    [ObservableProperty]
    private ObservableCollection<string> _waitlist = new();

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

    /// <summary>True while a chip tournament is still in progress - gates the director's per-player
    /// add/remove-chip controls in the standings (see ChipGameService.AdjustChips).</summary>
    [ObservableProperty]
    private bool _chipCanAdjust;

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

    /// <summary>True once the tournament has completed and a final finishing order exists - drives
    /// the "Final Results" column (Tournament tab and Display window). See RebuildFinalResults.</summary>
    [ObservableProperty]
    private bool _showFinalResults;

    /// <summary>Every entrant's final placement plus the prize their place earned, ordered by
    /// finish. Populated only once the tournament is Completed (empty otherwise).</summary>
    [ObservableProperty]
    private ObservableCollection<FinalResultRowViewModel> _finalResults = new();

    /// <summary>Whether to show the standalone "Prize Payouts" panel. It yields to the fuller
    /// "Final Results" column once the tournament completes (which already lists every place and
    /// its prize), so the two don't show the same money twice.</summary>
    public bool ShowPrizePayoutsPanel => ShowPrizePayouts && !ShowFinalResults;

    partial void OnShowPrizePayoutsChanged(bool value) => OnPropertyChanged(nameof(ShowPrizePayoutsPanel));

    partial void OnShowFinalResultsChanged(bool value) => OnPropertyChanged(nameof(ShowPrizePayoutsPanel));

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
            Waitlist = new ObservableCollection<string>();
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
            Waitlist = new ObservableCollection<string>();
            RingSeats = new ObservableCollection<RingSeatViewModel>();
            IsRingGame = false;
            RingStatusLine = string.Empty;
            ClearChipState();
            ClearPrizePayouts();
            return;
        }

        // Only elimination brackets can have a waitlist (double elimination's overflow); clear it up
        // front so the ring/chip/round-robin early returns below never show a stale list.
        Waitlist = new ObservableCollection<string>();

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

        Waitlist = new ObservableCollection<string>(
            tournament.Entrants
                .Where(e => e.IsWaitlisted)
                .OrderBy(e => e.SeedNumber ?? int.MaxValue)
                .Select(e => e.DisplayName));

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
        var availableTables = ComputeAvailableTables(tournament);

        foreach (var group in groups)
        {
            var matchRows = group
                .OrderBy(n => n.PositionInRound)
                .Select(n => n.Match is not null
                    ? new MatchRowViewModel(n.Match, tournament.SeedingRatingSystem, availableTables)
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
        ChipCanAdjust = tournament.Status != TournamentStatus.Completed;

        var pot = PrizePayoutService.TotalEntryFees(tournament).ToString("C0");
        var total = tournament.Entrants.Count;
        var active = rows.Count(r => !r.IsEliminated);

        if (tournament.Status == TournamentStatus.Completed)
        {
            var champion = rows.FirstOrDefault(r => r.Place == 1)?.Entrant.Player?.FullName ?? "-";
            ChipStatusLine = $"Finished  ·  Pot {pot}  ·  Winner: {champion}";
        }
        else if (tournament.ChipGame?.ChipRatingSystem is not null)
        {
            // Chips vary per player by skill range, so a single "N chips each" would be misleading.
            ChipStatusLine = $"{active} of {total} left  ·  chips by {tournament.ChipGame.ChipRatingSystem} rating  ·  Pot {pot}";
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
        ChipCanAdjust = false;
    }

    private void ClearPrizePayouts()
    {
        ShowPrizePayouts = false;
        PrizePoolSummaryLine = string.Empty;
        PrizePayouts = new ObservableCollection<PrizePayoutRowViewModel>();
        ClearFinalResults();
    }

    private void ClearFinalResults()
    {
        ShowFinalResults = false;
        FinalResults = new ObservableCollection<FinalResultRowViewModel>();
    }

    /// <summary>
    /// Rebuilds the "Final Results" list - every entrant's final placement and the prize that
    /// place earned - shown once the tournament is Completed. Reuses PrizePayoutService's placement
    /// logic (exact for Round Robin/Chip; champion/runner-up exact and lower places approximated by
    /// win/loss record for elimination brackets), but unlike the Prize Payouts panel it appears
    /// even when no prize places are configured (prizes then simply read blank).
    /// </summary>
    private void RebuildFinalResults(Tournament tournament)
    {
        // Modified Single Elimination is a qualifier format: each independent bracket crowns its own
        // winner (there is no single champion or prize order), so its Final Results simply list
        // every bracket winner as "Qualified".
        if (tournament.Format == TournamentFormat.ModifiedSingleElimination)
        {
            var qualifiers = tournament.Status == TournamentStatus.Completed
                ? PrizePayoutService.ComputeQualifiers(tournament)
                : new List<TournamentEntrant>();

            FinalResults = new ObservableCollection<FinalResultRowViewModel>(
                qualifiers.Select(FinalResultRowViewModel.Qualifier));
            ShowFinalResults = FinalResults.Count > 0;
            return;
        }

        var rows = tournament.Status == TournamentStatus.Completed
            ? PrizePayoutService.ComputeFinalResults(tournament)
            : new List<PrizePayoutRow>();

        FinalResults = new ObservableCollection<FinalResultRowViewModel>(
            rows.OrderBy(r => r.PlaceRangeStart).Select(r => new FinalResultRowViewModel(r)));
        ShowFinalResults = FinalResults.Count > 0;
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
        RebuildFinalResults(tournament);
        // Ring Game has its own money model; Modified Single Elimination is a qualifier format with
        // no prize pool (its Final Results list "Qualified" winners instead) - neither shows payouts.
        ShowPrizePayouts = tournament.Format is not (TournamentFormat.RingGame or TournamentFormat.ModifiedSingleElimination)
            && tournament.PrizePlaces.Count > 0;
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
        var availableTables = ComputeAvailableTables(tournament);

        foreach (var group in groups)
        {
            var matchRows = group.Select(m => new MatchRowViewModel(m, tournament.SeedingRatingSystem, availableTables)).ToList();
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
                // Each pod is an independent bracket: its Final Four (Final round 1) feeds its own
                // Bracket Final (Final round 2, one per pod - never a cross-pod stage).
                return roundNumber == maxRoundForSide ? "Bracket Final" : "Final Four";
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

    /// <summary>Tables not currently occupied by an in-progress match, in numerical order (by the
    /// number in each table's "Table N" label) - used to populate each scheduled match's
    /// table-picker ComboBox so an already-busy table can't be double-booked.</summary>
    private static List<Table> ComputeAvailableTables(Tournament tournament)
    {
        var tablesInUse = tournament.Matches
            .Where(m => m.Status == MatchStatus.InProgress && m.TableId is not null)
            .Select(m => m.TableId!.Value)
            .ToHashSet();
        return tournament.Tables
            .Where(t => !tablesInUse.Contains(t.Id))
            .OrderBy(TableLabelNumber)
            .ThenBy(t => t.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Extracts the numeric part of a table label (e.g. "Table 12" -> 12) so tables sort
    /// numerically instead of alphabetically ("Table 10" before "Table 2"). Falls back to
    /// int.MaxValue for a label with no digits, so it sorts after every numbered table.</summary>
    private static int TableLabelNumber(Table table)
    {
        var match = Regex.Match(table.Label, @"\d+");
        return match.Success ? int.Parse(match.Value) : int.MaxValue;
    }
}
