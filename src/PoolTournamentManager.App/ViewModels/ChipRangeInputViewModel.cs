using CommunityToolkit.Mvvm.ComponentModel;

namespace PoolTournamentManager.App.ViewModels;

/// <summary>
/// One editable "skill range → chips" row in the Chip Tournament setup form. A blank
/// <see cref="MinRating"/>/<see cref="MaxRating"/> means that side is unbounded (e.g. "650 and up"
/// leaves Max blank; "under 450" leaves Min blank). Maps to a Core ChipStartingRule when the
/// tournament is created/saved.
/// </summary>
public partial class ChipRangeInputViewModel : ObservableObject
{
    [ObservableProperty]
    private int? _minRating;

    [ObservableProperty]
    private int? _maxRating;

    [ObservableProperty]
    private int _chips = 3;
}
