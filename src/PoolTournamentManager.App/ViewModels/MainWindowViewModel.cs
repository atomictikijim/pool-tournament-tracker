using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoolTournamentManager.App.Services;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Interfaces;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IPlayerRepository _playerRepository;
    private readonly ITeamRepository _teamRepository;
    private Player? _editingPlayer;
    private Team? _editingTeam;

    public ObservableCollection<Player> Players { get; } = new();

    public ObservableCollection<Team> Teams { get; } = new();

    public PlayerEditorViewModel Editor { get; } = new();

    public TeamEditorViewModel TeamEditor { get; } = new();

    public TournamentViewModel Tournament { get; }

    public ThemeService Theme { get; }

    [ObservableProperty]
    private Player? _selectedPlayer;

    [ObservableProperty]
    private Team? _selectedTeam;

    [ObservableProperty]
    private string? _statusMessage;

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
    }

    partial void OnSelectedPlayerChanged(Player? value)
    {
        _editingPlayer = value;
        if (value is not null)
        {
            Editor.LoadFrom(value);
        }
    }

    partial void OnSelectedTeamChanged(Team? value)
    {
        _editingTeam = value;
        if (value is not null)
        {
            TeamEditor.LoadFrom(value);
        }
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

    [RelayCommand]
    public void AddNewPlayer()
    {
        SelectedPlayer = null;
        _editingPlayer = null;
        Editor.Reset();
        StatusMessage = "Enter details for the new player and click Save.";
    }

    [RelayCommand]
    public async Task SavePlayerAsync()
    {
        var candidate = _editingPlayer ?? new Player { FirstName = string.Empty, LastName = string.Empty };
        Editor.ApplyTo(candidate);

        var errors = PlayerValidator.Validate(candidate);
        if (errors.Count > 0)
        {
            StatusMessage = string.Join(" ", errors);
            return;
        }

        if (_editingPlayer is null)
        {
            await _playerRepository.AddAsync(candidate);
            StatusMessage = $"Added {candidate.FullName}.";
        }
        else
        {
            await _playerRepository.UpdateAsync(candidate);
            StatusMessage = $"Saved changes to {candidate.FullName}.";
        }

        await LoadPlayersAsync();
        SelectedPlayer = Players.FirstOrDefault(p => p.Id == candidate.Id);
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

    [RelayCommand]
    public void AddNewTeam()
    {
        SelectedTeam = null;
        _editingTeam = null;
        TeamEditor.Reset();
        StatusMessage = "Enter a name for the new team and click Save.";
    }

    [RelayCommand]
    public async Task SaveTeamAsync()
    {
        var candidate = _editingTeam ?? new Team { Name = string.Empty };
        TeamEditor.ApplyTo(candidate);

        var errors = TeamValidator.Validate(candidate);
        if (errors.Count > 0)
        {
            StatusMessage = string.Join(" ", errors);
            return;
        }

        if (_editingTeam is null)
        {
            await _teamRepository.AddAsync(candidate);
            StatusMessage = $"Added {candidate.Name}.";
        }
        else
        {
            await _teamRepository.UpdateAsync(candidate);
            StatusMessage = $"Saved changes to {candidate.Name}.";
        }

        await LoadTeamsAsync();
        SelectedTeam = Teams.FirstOrDefault(t => t.Id == candidate.Id);
    }
}
