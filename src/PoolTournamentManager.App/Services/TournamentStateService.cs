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

    /// <summary>Only the still-active players, for the winner/loser pickers.</summary>
    [ObservableProperty]
    private ObservableCollection<ChipStandingRowViewModel> _chipActiveEntrants = new();

    /// <summary>One-line chip-tournament status, e.g. "5 of 7 left  ·  3 chips each  ·  Pot $140".</summary>
    [ObservableProperty]
    private string _chipStatusLine = string.Empty;

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

        var rounds = new ObservableCollection<RoundGroupViewModel>();
        var bracket = tournament.Bracket;
        if (bracket is null || bracket.Nodes.Count == 0)
        {
            Rounds = rounds;
            return;
        }

        var sideRank = new Dictionary<BracketSide, int> { [BracketSide.Winners] = 0, [BracketSide.Losers] = 1, [BracketSide.GrandFinal] = 2 };
        var groups = bracket.Nodes
            .Where(n => n.MatchId is not null)
            .GroupBy(n => (n.Side, n.RoundNumber))
            .OrderBy(g => sideRank[g.Key.Side])
            .ThenBy(g => g.Key.RoundNumber);

        foreach (var group in groups)
        {
            var matchRows = group
                .OrderBy(n => n.PositionInRound)
                .Select(n => new MatchRowViewModel(n.Match!))
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
        ChipActiveEntrants = new ObservableCollection<ChipStandingRowViewModel>(
            ChipStandings.Where(r => !r.IsEliminated));

        var pot = ChipGameService.Pot(tournament).ToString("C0");
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
    }

    private void ClearChipState()
    {
        IsChipTournament = false;
        ChipStandings = new ObservableCollection<ChipStandingRowViewModel>();
        ChipActiveEntrants = new ObservableCollection<ChipStandingRowViewModel>();
        ChipStatusLine = string.Empty;
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
            var matchRows = group.Select(m => new MatchRowViewModel(m)).ToList();
            rounds.Add(new RoundGroupViewModel(group.Key, $"Round {group.Key}", matchRows));
        }

        Rounds = rounds;
        Standings = new ObservableCollection<StandingsRowViewModel>(
            RoundRobinStandingsService.ComputeStandings(tournament)
                .Select((row, index) => new StandingsRowViewModel(index + 1, row)));
    }

    private static string BuildRoundTitle(BracketDetail bracket, BracketSide side, int roundNumber, bool isReset)
    {
        if (side == BracketSide.GrandFinal)
        {
            return isReset ? "Bracket Reset" : "Grand Final";
        }

        var maxRoundForSide = bracket.Nodes.Where(n => n.Side == side).Max(n => n.RoundNumber);

        if (!bracket.IsDoubleElimination)
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
