using CommunityToolkit.Mvvm.ComponentModel;
using PoolTournamentManager.Core.Entities;

namespace PoolTournamentManager.App.ViewModels;

public partial class TeamSelectionItem : ObservableObject
{
    public Team Team { get; }

    [ObservableProperty]
    private bool _isSelected;

    public TeamSelectionItem(Team team)
    {
        Team = team;
    }
}
