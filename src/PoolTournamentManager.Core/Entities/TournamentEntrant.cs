namespace PoolTournamentManager.Core.Entities;

public class TournamentEntrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }
    public Guid? PlayerId { get; set; }
    public Player? Player { get; set; }
    public Guid? TeamId { get; set; }
    public Team? Team { get; set; }
    public int? SeedNumber { get; set; }
    public bool IsEliminated { get; set; }

    public string DisplayName => Player?.FullName ?? Team?.Name ?? "TBD";
}
