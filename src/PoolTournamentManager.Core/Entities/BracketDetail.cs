using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.Core.Entities;

public class BracketDetail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }
    public BracketKind Kind { get; set; }

    public List<BracketNode> Nodes { get; set; } = new();
}
