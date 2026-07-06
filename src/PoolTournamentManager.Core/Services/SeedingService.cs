using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.Core.Services;

public static class SeedingService
{
    /// <summary>
    /// Orders entrants by the chosen rating system (highest first) and assigns SeedNumber
    /// 1..N accordingly. Entrants missing a rating in the chosen system sort last, by name.
    /// </summary>
    public static List<TournamentEntrant> AssignSeeds(List<TournamentEntrant> entrants, RatingSystem ratingSystem)
    {
        var ordered = entrants
            .OrderByDescending(e => GetRating(e.Player, ratingSystem) ?? int.MinValue)
            .ThenBy(e => e.Player?.LastName)
            .ThenBy(e => e.Player?.FirstName)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].SeedNumber = i + 1;
        }

        return ordered;
    }

    public static bool HasRating(TournamentEntrant entrant, RatingSystem ratingSystem) =>
        GetRating(entrant.Player, ratingSystem) is not null;

    private static int? GetRating(Player? player, RatingSystem ratingSystem)
    {
        if (player is null)
        {
            return null;
        }

        return ratingSystem switch
        {
            RatingSystem.Fargo => player.FargoRate,
            RatingSystem.ApaEightBall => player.ApaEightBallSkill,
            RatingSystem.ApaNineBall => player.ApaNineBallSkill,
            RatingSystem.Tap => TryParseInt(player.TapRating),
            _ => null
        };
    }

    private static int? TryParseInt(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : null;
}
