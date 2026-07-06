using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.App.ViewModels;

public class RoundGroupViewModel
{
    public int RoundNumber { get; }
    public string Title { get; }
    public List<MatchRowViewModel> Matches { get; }

    /// <summary>
    /// Which half of the bracket this round belongs to (Winners/Losers/GrandFinal), or null for
    /// non-bracket formats (round robin). The bracket-tree layout uses this to stack the winners
    /// and losers halves into separate vertical bands.
    /// </summary>
    public BracketSide? Side { get; }

    public RoundGroupViewModel(int roundNumber, string title, List<MatchRowViewModel> matches, BracketSide? side = null)
    {
        RoundNumber = roundNumber;
        Title = title;
        Matches = matches;
        Side = side;
    }
}
