using PoolTournamentManager.Core.Entities;

namespace PoolTournamentManager.Core.Services;

public static class PlayerValidator
{
    private const int MinApaSkill = 1;
    private const int MaxApaSkill = 9;

    public static List<string> Validate(Player player)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(player.FirstName))
            errors.Add("First name is required.");

        if (string.IsNullOrWhiteSpace(player.LastName))
            errors.Add("Last name is required.");

        if (player.FargoRate is < 0)
            errors.Add("Fargo Rate cannot be negative.");

        if (player.ApaEightBallSkill is int eightBall && (eightBall < MinApaSkill || eightBall > MaxApaSkill))
            errors.Add($"APA 8-Ball skill level must be between {MinApaSkill} and {MaxApaSkill}.");

        if (player.ApaNineBallSkill is int nineBall && (nineBall < MinApaSkill || nineBall > MaxApaSkill))
            errors.Add($"APA 9-Ball skill level must be between {MinApaSkill} and {MaxApaSkill}.");

        return errors;
    }
}
