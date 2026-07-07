namespace PoolTournamentManager.Core.Entities;

public class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
}
