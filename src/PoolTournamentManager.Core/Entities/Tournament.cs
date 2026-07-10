using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.Core.Entities;

public class Tournament
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public GameType GameType { get; set; }
    public TournamentFormat Format { get; set; }
    public TournamentStatus Status { get; set; } = TournamentStatus.NotStarted;
    public RatingSystem? SeedingRatingSystem { get; set; }
    public bool UsesTeams { get; set; }

    /// <summary>Per-entrant entry fee. Not used by Ring Game, which has its own buy-in.</summary>
    public decimal EntryFee { get; set; }

    /// <summary>Percentage of total entry fees kept by the tournament host, taken off the top
    /// before the remaining prize pool is split across <see cref="PrizePlaces"/>.</summary>
    public decimal HostFeePercentage { get; set; }

    public List<TournamentEntrant> Entrants { get; set; } = new();
    public List<Table> Tables { get; set; } = new();
    public List<Match> Matches { get; set; } = new();
    public List<TournamentPrizePlace> PrizePlaces { get; set; } = new();
    public BracketDetail? Bracket { get; set; }
    public RingGameDetail? RingGame { get; set; }
    public ChipGameDetail? ChipGame { get; set; }
}
