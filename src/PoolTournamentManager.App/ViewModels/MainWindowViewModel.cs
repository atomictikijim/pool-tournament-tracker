using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoolTournamentManager.App.Services;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Interfaces;

namespace PoolTournamentManager.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IPlayerRepository _playerRepository;
    private readonly ITeamRepository _teamRepository;

    public ObservableCollection<Player> Players { get; } = new();

    public ObservableCollection<Team> Teams { get; } = new();

    public TournamentViewModel Tournament { get; }

    public ThemeService Theme { get; }

    [ObservableProperty]
    private Player? _selectedPlayer;

    [ObservableProperty]
    private Team? _selectedTeam;

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>Index of the selected tab on the main window's TabControl (0=Players, 1=Teams,
    /// 2=Tournament, 3=Tournament Settings). Set from code-behind when "Edit Tournament" is
    /// clicked, and automatically to 2 whenever a tournament is created or saved (see
    /// TournamentViewModel.TournamentReady) so the operator lands on the tournament they just
    /// built without an extra manual click.</summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    public MainWindowViewModel(
        IPlayerRepository playerRepository,
        ITeamRepository teamRepository,
        TournamentViewModel tournamentViewModel,
        ThemeService themeService)
    {
        _playerRepository = playerRepository;
        _teamRepository = teamRepository;
        Tournament = tournamentViewModel;
        Theme = themeService;
        Tournament.TournamentReady += () => SelectedTabIndex = 2;
    }

    [RelayCommand]
    public async Task LoadPlayersAsync()
    {
        var players = await _playerRepository.GetAllAsync();
        Players.Clear();
        foreach (var player in players)
        {
            Players.Add(player);
        }
        StatusMessage = $"Loaded {Players.Count} player(s).";
    }

    /// <summary>
    /// Persists a brand-new player built by the modal editor. The dialog has already validated the
    /// input, so this just applies and saves, then reselects the added row.
    /// </summary>
    public async Task CreatePlayerAsync(PlayerEditorViewModel editor)
    {
        var candidate = new Player { FirstName = string.Empty, LastName = string.Empty };
        editor.ApplyTo(candidate);
        await _playerRepository.AddAsync(candidate);
        await LoadPlayersAsync();
        SelectedPlayer = Players.FirstOrDefault(p => p.Id == candidate.Id);
        StatusMessage = $"Added {candidate.FullName}.";
    }

    /// <summary>Persists edits made in the modal editor back onto an existing player.</summary>
    public async Task UpdatePlayerAsync(Player target, PlayerEditorViewModel editor)
    {
        editor.ApplyTo(target);
        await _playerRepository.UpdateAsync(target);
        await LoadPlayersAsync();
        SelectedPlayer = Players.FirstOrDefault(p => p.Id == target.Id);
        StatusMessage = $"Saved changes to {target.FullName}.";
    }

    /// <summary>
    /// Deletes each selected player, skipping any that are still entered in a tournament (blocked
    /// by the entrant foreign key). Confirmation is handled by the caller before this runs.
    /// </summary>
    public async Task DeletePlayersAsync(IReadOnlyList<Player> players)
    {
        if (players.Count == 0)
        {
            StatusMessage = "Select one or more players to delete.";
            return;
        }

        var blocked = new List<string>();
        var deleted = 0;
        foreach (var player in players)
        {
            if (await _playerRepository.IsReferencedAsync(player.Id))
            {
                blocked.Add(player.FullName);
                continue;
            }
            await _playerRepository.DeleteAsync(player);
            deleted++;
        }

        await LoadPlayersAsync();
        StatusMessage = ComposeDeleteStatus("player", deleted, blocked);
    }

    [RelayCommand]
    public async Task LoadTeamsAsync()
    {
        var teams = await _teamRepository.GetAllAsync();
        Teams.Clear();
        foreach (var team in teams)
        {
            Teams.Add(team);
        }
        StatusMessage = $"Loaded {Teams.Count} team(s).";
    }

    /// <summary>Persists a brand-new team built by the modal editor.</summary>
    public async Task CreateTeamAsync(TeamEditorViewModel editor)
    {
        var candidate = new Team { Name = string.Empty };
        editor.ApplyTo(candidate);
        await _teamRepository.AddAsync(candidate);
        await LoadTeamsAsync();
        SelectedTeam = Teams.FirstOrDefault(t => t.Id == candidate.Id);
        StatusMessage = $"Added {candidate.Name}.";
    }

    /// <summary>Persists edits made in the modal editor back onto an existing team.</summary>
    public async Task UpdateTeamAsync(Team target, TeamEditorViewModel editor)
    {
        editor.ApplyTo(target);
        await _teamRepository.UpdateAsync(target);
        await LoadTeamsAsync();
        SelectedTeam = Teams.FirstOrDefault(t => t.Id == target.Id);
        StatusMessage = $"Saved changes to {target.Name}.";
    }

    /// <summary>
    /// Deletes each selected team, skipping any that are still entered in a tournament (blocked by
    /// the entrant foreign key). Confirmation is handled by the caller before this runs.
    /// </summary>
    public async Task DeleteTeamsAsync(IReadOnlyList<Team> teams)
    {
        if (teams.Count == 0)
        {
            StatusMessage = "Select one or more teams to delete.";
            return;
        }

        var blocked = new List<string>();
        var deleted = 0;
        foreach (var team in teams)
        {
            if (await _teamRepository.IsReferencedAsync(team.Id))
            {
                blocked.Add(team.Name);
                continue;
            }
            await _teamRepository.DeleteAsync(team);
            deleted++;
        }

        await LoadTeamsAsync();
        StatusMessage = ComposeDeleteStatus("team", deleted, blocked);
    }

    private static string ComposeDeleteStatus(string noun, int deleted, List<string> blocked)
    {
        var parts = new List<string>();
        if (deleted > 0)
        {
            parts.Add($"Deleted {deleted} {noun}(s).");
        }
        if (blocked.Count > 0)
        {
            parts.Add($"Could not delete {string.Join(", ", blocked)} - still entered in a tournament.");
        }
        return parts.Count > 0 ? string.Join(" ", parts) : $"No {noun}s were deleted.";
    }
}
