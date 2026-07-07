using CommunityToolkit.Mvvm.ComponentModel;
using PoolTournamentManager.Core.Entities;

namespace PoolTournamentManager.App.ViewModels;

public partial class TeamEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    public void LoadFrom(Team team)
    {
        Name = team.Name;
    }

    public void Reset()
    {
        Name = string.Empty;
    }

    public void ApplyTo(Team team)
    {
        team.Name = Name;
    }
}
