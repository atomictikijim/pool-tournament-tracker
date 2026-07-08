using CommunityToolkit.Mvvm.ComponentModel;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;
using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.App.ViewModels;

public partial class PlayerSelectionItem : ObservableObject
{
    public Player Player { get; }

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>The rating system currently chosen by the tournament's "Seed by rating" control
    /// (null when that control is hidden/inapplicable), pushed in by TournamentViewModel so this
    /// candidate's checklist label can show the matching rating.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayLabel))]
    private RatingSystem? _ratingSystem;

    public PlayerSelectionItem(Player player)
    {
        Player = player;
    }

    /// <summary>"Alice Anderson" normally, or "Alice Anderson (Fargo: 700)" while a rating system
    /// is selected - "—" in place of the number when this player has no rating recorded.</summary>
    public string DisplayLabel
    {
        get
        {
            if (RatingSystem is null)
            {
                return Player.FullName;
            }

            var rating = SeedingService.GetRatingDisplay(Player, RatingSystem.Value) ?? "—";
            return $"{Player.FullName} ({SeedingService.GetRatingLabel(RatingSystem.Value)}: {rating})";
        }
    }
}
