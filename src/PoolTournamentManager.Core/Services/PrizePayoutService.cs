using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.Core.Services;

/// <summary>One entrant's share of the prize pool, computed from Tournament.PrizePlaces.</summary>
public class PrizePayoutRow
{
    public required TournamentEntrant Entrant { get; init; }

    /// <summary>1-based finishing place. Equal to <see cref="PlaceRangeEnd"/> unless this
    /// entrant is tied with others for a shared range of places (e.g. two semifinal losers
    /// tied for 3rd-4th), in which case they split that range's combined payout evenly.</summary>
    public int PlaceRangeStart { get; init; }
    public int PlaceRangeEnd { get; init; }

    public decimal Payout { get; init; }
}

/// <summary>
/// Computes "who gets paid what" from a Tournament's EntryFee/HostFeePercentage/PrizePlaces.
/// Not used for Ring Game, which has its own continuous buy-in/per-ball-payout model with no
/// discrete finishing order.
///
/// Round Robin and Chip Tournament already have an exact, never-tied placement (see
/// RoundRobinStandingsService/ChipGameService) reused as-is. Elimination brackets have no
/// placement concept at all beyond the champion/runner-up (the deciding match), so 3rd place
/// and below are approximated by match win/loss record: entrants with identical records tie
/// and split the combined payout for the place range they occupy. This is a deliberate
/// simplification, not exact bracket-depth traversal - see NOTES.md.
/// </summary>
public static class PrizePayoutService
{
    /// <summary>Gross money collected: entry fee times entrant count.</summary>
    public static decimal TotalEntryFees(Tournament tournament) => tournament.EntryFee * tournament.Entrants.Count;

    /// <summary>The portion of total entry fees kept by the tournament host.</summary>
    public static decimal HostCut(Tournament tournament) => TotalEntryFees(tournament) * (tournament.HostFeePercentage / 100m);

    /// <summary>What's left to award across the configured prize places.</summary>
    public static decimal PrizePool(Tournament tournament) => TotalEntryFees(tournament) - HostCut(tournament);

    /// <summary>
    /// The per-entrant prize breakdown for the configured prize places. Returns empty when no
    /// prize places are configured (there's no money to split) - use <see cref="ComputeFinalResults"/>
    /// when you want every entrant's finishing placement regardless of payouts.
    /// </summary>
    public static List<PrizePayoutRow> ComputePayouts(Tournament tournament)
    {
        // Modified Single Elimination is a qualifier format (each pod crowns a winner who advances
        // to a higher event) with no prize-pool concept - see ComputeQualifiers. Ring Game has its
        // own separate money model.
        if (tournament.Format is TournamentFormat.RingGame or TournamentFormat.ModifiedSingleElimination
            || tournament.PrizePlaces.Count == 0)
        {
            return new List<PrizePayoutRow>();
        }

        return ComputeRows(tournament);
    }

    /// <summary>
    /// The winning entrant of each independent bracket in a Modified Single Elimination tournament
    /// (one per pod) - the entrants who "qualified". These are co-equal (there is no single overall
    /// champion), ordered by bracket. Empty for any other format, or until the tournament completes.
    /// </summary>
    public static List<TournamentEntrant> ComputeQualifiers(Tournament tournament)
    {
        if (tournament.Format != TournamentFormat.ModifiedSingleElimination
            || tournament.Status != TournamentStatus.Completed
            || tournament.Bracket is null)
        {
            return new List<TournamentEntrant>();
        }

        var entrantsById = tournament.Entrants.ToDictionary(e => e.Id);
        return tournament.Bracket.Nodes
            .Where(n => n.Side == BracketSide.Final
                        && n.FeedsIntoWinnerNodeId is null
                        && n.Match is { Status: MatchStatus.Completed, WinnerEntrantId: not null })
            .OrderBy(n => n.PositionInRound)
            .Select(n => entrantsById[n.Match!.WinnerEntrantId!.Value])
            .ToList();
    }

    /// <summary>
    /// Every entrant's final finishing placement plus the prize their place earned - the prize is
    /// zero when no prize places are configured, or when their place isn't a funded one. Unlike
    /// <see cref="ComputePayouts"/> this never returns empty just because no prize places exist;
    /// it's the full "final standings" list shown when a tournament completes. Ordered by the
    /// underlying placement. Empty for Ring Game (no discrete finishing order) and for elimination
    /// brackets that haven't completed yet.
    /// </summary>
    public static List<PrizePayoutRow> ComputeFinalResults(Tournament tournament)
    {
        if (tournament.Format == TournamentFormat.RingGame)
        {
            return new List<PrizePayoutRow>();
        }

        // Modified Single Elimination is a qualifier format: its final results are simply each
        // independent bracket's winner (co-equal 1st places), never a prize order - see
        // ComputeQualifiers. Any configured prize places are ignored for this format.
        if (tournament.Format == TournamentFormat.ModifiedSingleElimination)
        {
            return ComputeQualifiers(tournament)
                .Select(entrant => new PrizePayoutRow
                {
                    Entrant = entrant,
                    PlaceRangeStart = 1,
                    PlaceRangeEnd = 1,
                    Payout = 0m
                })
                .ToList();
        }

        return ComputeRows(tournament);
    }

    /// <summary>Shared placement-to-payout projection behind both public entry points: turns each
    /// placement group into one row per entrant, splitting the group's combined funded percentage
    /// evenly across its members (zero when none of the group's places are funded).</summary>
    private static List<PrizePayoutRow> ComputeRows(Tournament tournament)
    {
        var placements = ComputePlacements(tournament);
        if (placements.Count == 0)
        {
            return new List<PrizePayoutRow>();
        }

        var pool = PrizePool(tournament);
        var percentageByPlace = tournament.PrizePlaces.ToDictionary(p => p.Place, p => p.Percentage);

        var rows = new List<PrizePayoutRow>();
        foreach (var group in placements)
        {
            var groupPercentage = 0m;
            for (var place = group.RangeStart; place <= group.RangeEnd; place++)
            {
                groupPercentage += percentageByPlace.GetValueOrDefault(place);
            }

            var perEntrant = group.Entrants.Count > 0 ? pool * groupPercentage / 100m / group.Entrants.Count : 0m;
            rows.AddRange(group.Entrants.Select(entrant => new PrizePayoutRow
            {
                Entrant = entrant,
                PlaceRangeStart = group.RangeStart,
                PlaceRangeEnd = group.RangeEnd,
                Payout = perEntrant
            }));
        }

        return rows;
    }

    private sealed record PlacementGroup(List<TournamentEntrant> Entrants, int RangeStart, int RangeEnd);

    // Modified Single Elimination isn't listed here: it never reaches ComputeRows/ComputePlacements
    // (ComputePayouts and ComputeFinalResults both short-circuit it as a qualifier format above).
    private static List<PlacementGroup> ComputePlacements(Tournament tournament) => tournament.Format switch
    {
        TournamentFormat.RoundRobin => RoundRobinPlacements(tournament),
        TournamentFormat.ChipTournament => ChipPlacements(tournament),
        TournamentFormat.SingleElimination or TournamentFormat.DoubleElimination
            => BracketPlacements(tournament),
        _ => new List<PlacementGroup>()
    };

    private static List<PlacementGroup> RoundRobinPlacements(Tournament tournament) =>
        RoundRobinStandingsService.ComputeStandings(tournament)
            .Select((row, index) => new PlacementGroup(new List<TournamentEntrant> { row.Entrant }, index + 1, index + 1))
            .ToList();

    private static List<PlacementGroup> ChipPlacements(Tournament tournament) =>
        ChipGameService.ComputeStandings(tournament)
            .Where(row => row.Place is not null)
            .Select(row => new PlacementGroup(new List<TournamentEntrant> { row.Entrant }, row.Place!.Value, row.Place!.Value))
            .ToList();

    private static List<PlacementGroup> BracketPlacements(Tournament tournament)
    {
        if (tournament.Status != TournamentStatus.Completed || tournament.Bracket is null)
        {
            return new List<PlacementGroup>();
        }

        var (championId, runnerUpId) = FindFinalists(tournament.Bracket);
        if (championId is null)
        {
            return new List<PlacementGroup>();
        }

        var groups = new List<PlacementGroup>
        {
            new(new List<TournamentEntrant> { tournament.Entrants.First(e => e.Id == championId.Value) }, 1, 1)
        };

        var remaining = tournament.Entrants.Where(e => e.Id != championId.Value).ToList();
        var nextPlace = 2;

        if (runnerUpId is not null)
        {
            groups.Add(new PlacementGroup(new List<TournamentEntrant> { tournament.Entrants.First(e => e.Id == runnerUpId.Value) }, 2, 2));
            remaining = remaining.Where(e => e.Id != runnerUpId.Value).ToList();
            nextPlace = 3;
        }

        var record = remaining.ToDictionary(e => e.Id, e => MatchRecord(tournament, e.Id));
        var ranked = remaining
            .OrderByDescending(e => record[e.Id].Wins)
            .ThenBy(e => record[e.Id].Losses)
            .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var i = 0;
        while (i < ranked.Count)
        {
            var currentRecord = record[ranked[i].Id];
            var tieGroup = new List<TournamentEntrant> { ranked[i] };
            var j = i + 1;
            while (j < ranked.Count && record[ranked[j].Id] == currentRecord)
            {
                tieGroup.Add(ranked[j]);
                j++;
            }

            groups.Add(new PlacementGroup(tieGroup, nextPlace, nextPlace + tieGroup.Count - 1));
            nextPlace += tieGroup.Count;
            i = j;
        }

        return groups;
    }

    /// <summary>Match wins/losses for one entrant, excluding byes (a bye isn't a played match).</summary>
    private static (int Wins, int Losses) MatchRecord(Tournament tournament, Guid entrantId)
    {
        var wins = 0;
        var losses = 0;
        foreach (var match in tournament.Matches)
        {
            if (match.IsBye || match.Status != MatchStatus.Completed || match.WinnerEntrantId is null)
            {
                continue;
            }
            if (match.Player1EntrantId != entrantId && match.Player2EntrantId != entrantId)
            {
                continue;
            }

            if (match.WinnerEntrantId == entrantId)
            {
                wins++;
            }
            else
            {
                losses++;
            }
        }

        return (wins, losses);
    }

    /// <summary>
    /// Finds the tournament-deciding match's winner/loser, per BracketKind: the top Winners-side
    /// node for Single Elimination, or the Grand Final (preferring its bracket-reset rematch if one
    /// was played) for Double Elimination. Returns (null, null) if the deciding match hasn't
    /// completed yet. Not used for Modified Single Elimination, which has no single champion - its
    /// per-pod winners come from <see cref="ComputeQualifiers"/> instead.
    /// </summary>
    private static (Guid? Champion, Guid? RunnerUp) FindFinalists(BracketDetail bracket)
    {
        var finalNode = bracket.Kind switch
        {
            BracketKind.SingleElimination =>
                bracket.Nodes.FirstOrDefault(n => n.Side == BracketSide.Winners && n.FeedsIntoWinnerNodeId is null),
            BracketKind.DoubleElimination => bracket.Nodes
                .Where(n => n.Side == BracketSide.GrandFinal && n.Match is { Status: MatchStatus.Completed })
                .OrderByDescending(n => n.IsGrandFinalReset)
                .FirstOrDefault(),
            _ => null
        };

        if (finalNode?.Match is not { Status: MatchStatus.Completed, WinnerEntrantId: not null } match)
        {
            return (null, null);
        }

        var championId = match.WinnerEntrantId!.Value;
        var runnerUpId = championId == match.Player1EntrantId ? match.Player2EntrantId : match.Player1EntrantId;
        return (championId, runnerUpId);
    }
}
