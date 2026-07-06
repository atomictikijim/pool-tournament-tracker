using CommunityToolkit.Mvvm.ComponentModel;
using PoolTournamentManager.Core.Entities;

namespace PoolTournamentManager.App.ViewModels;

public partial class PlayerSelectionItem : ObservableObject
{
    public Player Player { get; }

    [ObservableProperty]
    private bool _isSelected;

    public PlayerSelectionItem(Player player)
    {
        Player = player;
    }
}
