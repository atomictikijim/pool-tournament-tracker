namespace PoolTournamentManager.Core.Entities;

public class Table
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }
    public required string Label { get; set; }
    public bool IsActive { get; set; } = true;
}
