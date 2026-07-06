using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Interfaces;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IPlayerRepository _playerRepository;
    private Player? _editingPlayer;

    public ObservableCollection<Player> Players { get; } = new();

    public PlayerEditorViewModel Editor { get; } = new();

    public TournamentViewModel Tournament { get; }

    [ObservableProperty]
    private Player? _selectedPlayer;

    [ObservableProperty]
    private string? _statusMessage;

    public MainWindowViewModel(IPlayerRepository playerRepository, TournamentViewModel tournamentViewModel)
    {
        _playerRepository = playerRepository;
        Tournament = tournamentViewModel;
    }

    partial void OnSelectedPlayerChanged(Player? value)
    {
        _editingPlayer = value;
        if (value is not null)
        {
            Editor.LoadFrom(value);
        }
        DeactivatePlayerCommand.NotifyCanExecuteChanged();
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

    [RelayCommand(CanExecute = nameof(CanDeactivatePlayer))]
    public async Task DeactivatePlayerAsync()
    {
        if (_editingPlayer is null)
        {
            return;
        }

        var deactivatedPlayerId = _editingPlayer.Id;
        _editingPlayer.IsActive = false;
        await _playerRepository.UpdateAsync(_editingPlayer);
        StatusMessage = $"Deactivated {_editingPlayer.FullName}.";

        await LoadPlayersAsync();
        SelectedPlayer = Players.FirstOrDefault(p => p.Id == deactivatedPlayerId);
    }

    public bool CanDeactivatePlayer() => _editingPlayer is { IsActive: true };
}
