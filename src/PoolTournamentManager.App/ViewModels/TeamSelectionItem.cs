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

    /// <summary>"Sharks" normally, or with the team's Division and/or Location appended in
    /// parentheses when present - e.g. "Sharks (Div A · Corner Pocket)", "Sharks (Div A)", or
    /// "Sharks (Corner Pocket)". Mirrors how the individual-Player checklist shows a rating.</summary>
    public string DisplayLabel
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Team.Division))
            {
                parts.Add($"Div {Team.Division}");
            }
            if (!string.IsNullOrWhiteSpace(Team.Location))
            {
                parts.Add(Team.Location);
            }
            return parts.Count > 0 ? $"{Team.Name} ({string.Join(" · ", parts)})" : Team.Name;
        }
    }
}
