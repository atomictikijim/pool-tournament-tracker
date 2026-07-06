namespace PoolTournamentManager.Core.Entities;

public class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    public int? FargoRate { get; set; }
    public string? TapRating { get; set; }
    public int? ApaEightBallSkill { get; set; }
    public int? ApaNineBallSkill { get; set; }

    public bool IsActive { get; set; } = true;

    public string FullName => $"{FirstName} {LastName}";
}
