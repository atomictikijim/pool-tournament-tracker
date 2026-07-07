using PoolTournamentManager.Core.Entities;

namespace PoolTournamentManager.Core.Services;

public static class TeamValidator
{
    public static List<string> Validate(Team team)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(team.Name))
            errors.Add("Team name is required.");

        return errors;
    }
}
