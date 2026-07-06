namespace PoolTournamentManager.App.ViewModels;

public class RoundGroupViewModel
{
    public int RoundNumber { get; }
    public string Title { get; }
    public List<MatchRowViewModel> Matches { get; }

    public RoundGroupViewModel(int roundNumber, string title, List<MatchRowViewModel> matches)
    {
        RoundNumber = roundNumber;
        Title = title;
        Matches = matches;
    }
}
