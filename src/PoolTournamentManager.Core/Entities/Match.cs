using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.Core.Entities;

public class Match
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }
    public Guid? BracketNodeId { get; set; }
    public Guid? TableId { get; set; }

    /// <summary>Round-robin round number this match belongs to. Unused by bracket formats, which
    /// track round via BracketNode.RoundNumber instead.</summary>
    public int? RoundNumber { get; set; }

    public Guid Player1EntrantId { get; set; }
    public Guid? Player2EntrantId { get; set; }
    public int? Player1Score { get; set; }
    public int? Player2Score { get; set; }
    public Guid? WinnerEntrantId { get; set; }
    public MatchStatus Status { get; set; } = MatchStatus.Scheduled;

    public TournamentEntrant? Player1Entrant { get; set; }
    public TournamentEntrant? Player2Entrant { get; set; }
    public Table? Table { get; set; }

    public bool IsBye => Player2EntrantId is null;
}
