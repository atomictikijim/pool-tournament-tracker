using CommunityToolkit.Mvvm.ComponentModel;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.App.ViewModels;

public partial class TeamEditorViewModel : ObservableObject
{
    /// <summary>Heading shown in the modal editor window ("New Team" / "Edit Team").</summary>
    [ObservableProperty]
    private string _title = "Team";

    /// <summary>Validation errors for the current fields; null when the input is valid.</summary>
    [ObservableProperty]
    private string? _errorMessage;

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

    /// <summary>
    /// Validates the current field values via <see cref="TeamValidator"/>, populating
    /// <see cref="ErrorMessage"/> on failure. The modal editor calls this before closing so the
    /// user sees inline errors instead of committing an invalid record.
    /// </summary>
    public bool TryValidate()
    {
        var scratch = new Team { Name = string.Empty };
        ApplyTo(scratch);
        var errors = TeamValidator.Validate(scratch);
        ErrorMessage = errors.Count > 0 ? string.Join(Environment.NewLine, errors) : null;
        return errors.Count == 0;
    }
}
