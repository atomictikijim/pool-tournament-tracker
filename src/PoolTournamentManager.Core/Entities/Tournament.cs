using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.Core.Entities;

public class Tournament
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public GameType GameType { get; set; }
    public TournamentFormat Format { get; set; }
    public TournamentStatus Status { get; set; } = TournamentStatus.Setup;
    public RatingSystem? SeedingRatingSystem { get; set; }

    public List<TournamentEntrant> Entrants { get; set; } = new();
    public List<Table> Tables { get; set; } = new();
    public List<Match> Matches { get; set; } = new();
    public BracketDetail? Bracket { get; set; }
    public RingGameDetail? RingGame { get; set; }
}
