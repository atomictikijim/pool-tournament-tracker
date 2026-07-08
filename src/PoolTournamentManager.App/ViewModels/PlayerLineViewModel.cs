namespace PoolTournamentManager.App.ViewModels;

/// <summary>
/// One player's line inside a bracket match box: their seed, name, score, and whether they won.
/// Kept as a tiny read-only projection so the display's winner-highlight can be driven entirely
/// by a DataTrigger on <see cref="IsWinner"/> against named template parts, instead of local
/// property values that would defeat the trigger (see NOTES.md precedence traps).
/// </summary>
public sealed class PlayerLineViewModel
{
    public string Name { get; }
    public string ScoreDisplay { get; }
    public bool IsWinner { get; }
    public string SeedDisplay { get; }
    public bool HasSeed { get; }
    public string RatingDisplay { get; }
    public bool HasRating { get; }

    public PlayerLineViewModel(string name, int? score, bool isWinner, int? seed, string? rating = null)
    {
        Name = name;
        ScoreDisplay = score?.ToString() ?? string.Empty;
        IsWinner = isWinner;
        HasSeed = seed is not null;
        SeedDisplay = seed?.ToString() ?? string.Empty;
        HasRating = rating is not null;
        RatingDisplay = rating ?? string.Empty;
    }
}
