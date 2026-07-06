namespace PoolTournamentManager.App.ViewModels;

/// <summary>
/// One player's line inside a bracket match box: their name, score, and whether they won.
/// Kept as a tiny read-only projection so the display's winner-highlight can be driven entirely
/// by a DataTrigger on <see cref="IsWinner"/> against named template parts, instead of local
/// property values that would defeat the trigger (see NOTES.md precedence traps).
/// </summary>
public sealed class PlayerLineViewModel
{
    public string Name { get; }
    public string ScoreDisplay { get; }
    public bool IsWinner { get; }

    public PlayerLineViewModel(string name, int? score, bool isWinner)
    {
        Name = name;
        ScoreDisplay = score?.ToString() ?? string.Empty;
        IsWinner = isWinner;
    }
}
