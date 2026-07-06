using PoolTournamentManager.Core.Services;

namespace PoolTournamentManager.App.ViewModels;

public class StandingsRowViewModel
{
    public int Rank { get; }
    public string PlayerName { get; }
    public int Wins { get; }
    public int Losses { get; }
    public int PointDifferential { get; }
    public string GamesWonPercentageDisplay { get; }

    public StandingsRowViewModel(int rank, RoundRobinStandingRow row)
    {
        Rank = rank;
        PlayerName = row.Entrant.Player?.FullName ?? "Unknown";
        Wins = row.Wins;
        Losses = row.Losses;
        PointDifferential = row.PointDifferential;
        GamesWonPercentageDisplay = row.GamesWonPercentage.ToString("P0");
    }
}
