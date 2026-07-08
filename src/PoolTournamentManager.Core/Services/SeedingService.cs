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
            .OrderByDescending(e => GetRatingValue(e.Player, ratingSystem) ?? int.MinValue)
            .ThenBy(e => e.DisplayName)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].SeedNumber = i + 1;
        }

        return ordered;
    }

    public static bool HasRating(TournamentEntrant entrant, RatingSystem ratingSystem) =>
        GetRatingValue(entrant.Player, ratingSystem) is not null;

    /// <summary>The player's rating value for the given system as a number (TAP is parsed from
    /// its raw string), or null if there's no Player or no parseable rating recorded in that
    /// system. Used for numeric comparisons - seeding order, min/max range filters.</summary>
    public static int? GetRatingValue(Player? player, RatingSystem ratingSystem) => ratingSystem switch
    {
        RatingSystem.Fargo => player?.FargoRate,
        RatingSystem.ApaEightBall => player?.ApaEightBallSkill,
        RatingSystem.ApaNineBall => player?.ApaNineBallSkill,
        RatingSystem.Tap => TryParseInt(player?.TapRating),
        _ => null
    };

    /// <summary>The entrant's rating value for the given system, formatted for display (e.g. a
    /// Fargo/APA number or a raw TAP string), or null if the entrant has no Player or no rating
    /// recorded in that system.</summary>
    public static string? GetRatingDisplay(Player? player, RatingSystem ratingSystem) => ratingSystem switch
    {
        RatingSystem.Fargo => player?.FargoRate?.ToString(),
        RatingSystem.ApaEightBall => player?.ApaEightBallSkill?.ToString(),
        RatingSystem.ApaNineBall => player?.ApaNineBallSkill?.ToString(),
        RatingSystem.Tap => player?.TapRating,
        _ => null
    };

    /// <summary>Short human-readable label for a rating system, e.g. "APA 8-Ball".</summary>
    public static string GetRatingLabel(RatingSystem ratingSystem) => ratingSystem switch
    {
        RatingSystem.Fargo => "Fargo",
        RatingSystem.ApaEightBall => "APA 8-Ball",
        RatingSystem.ApaNineBall => "APA 9-Ball",
        RatingSystem.Tap => "TAP",
        _ => ratingSystem.ToString()
    };

    private static int? TryParseInt(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : null;
}
