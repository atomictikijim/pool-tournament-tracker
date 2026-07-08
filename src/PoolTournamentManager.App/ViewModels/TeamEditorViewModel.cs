using CommunityToolkit.Mvvm.ComponentModel;
using PoolTournamentManager.Core.Entities;

namespace PoolTournamentManager.App.ViewModels;

public partial class TeamEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _division;

    [ObservableProperty]
    private string? _location;

    public void LoadFrom(Team team)
    {
        Name = team.Name;
        Division = team.Division;
        Location = team.Location;
    }

    public void Reset()
    {
        Name = string.Empty;
        Division = null;
        Location = null;
    }

    public void ApplyTo(Team team)
    {
        team.Name = Name;
        team.Division = Division;
        team.Location = Location;
    }
}
