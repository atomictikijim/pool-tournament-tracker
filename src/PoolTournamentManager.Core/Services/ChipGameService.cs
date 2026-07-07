using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.Core.Services;

/// <summary>A single player's standing in a chip tournament, computed from the game log.</summary>
public class ChipStandingRow
{
    public required TournamentEntrant Entrant { get; init; }
    public int ChipsRemaining { get; init; }
    public bool IsEliminated { get; init; }

    /// <summary>Finishing place (1 = champion) once decided; null while still in contention.</summary>
    public int? Place { get; init; }

    /// <summary>Prize for this finishing place (0 if unplaced or out of the money).</summary>
    public decimal Payout { get; init; }
}

/// <summary>
/// Drives a "lives" chip tournament: every player starts with the same number of chips, and each
/// recorded game (ad-hoc between any two players who still have chips) costs the loser one chip -
/// the winner's count is unchanged. A player at 0 chips is eliminated; the last player still
/// holding a chip wins. Chip counts and finishing places are always recomputed from the game log,
/// never stored, so any game can be replayed or (in future) undone from the ledger alone.
/// </summary>
public class ChipGameService
{
    /// <summary>
    /// Sets up a chip tournament on an already-populated tournament: gives every entrant the same
    /// starting chip count, records the buy-in/payout settings, and marks the tournament InProgress.
    /// </summary>
    public ChipGameDetail StartChipTournament(
        Tournament tournament, int startingChips, decimal buyIn, decimal firstPayout, decimal secondPayout, decimal thirdPayout)
    {
        if (tournament.Entrants.Count < 2)
        {
            throw new InvalidOperationException("A chip tournament needs at least 2 players.");
        }
        if (startingChips < 1)
        {
            throw new InvalidOperationException("Each player needs at least 1 starting chip.");
        }

        var detail = new ChipGameDetail
        {
            TournamentId = tournament.Id,
            StartingChips = startingChips,
            BuyInAmount = buyIn,
            FirstPlacePayout = firstPayout,
            SecondPlacePayout = secondPayout,
            ThirdPlacePayout = thirdPayout
        };

        foreach (var entrant in tournament.Entrants)
        {
            entrant.IsEliminated = false;
        }

        tournament.ChipGame = detail;
        tournament.Status = TournamentStatus.InProgress;
        return detail;
    }

    /// <summary>
    /// Records that <paramref name="winnerId"/> beat <paramref name="loserId"/>: the loser drops a
    /// chip (and is eliminated if that empties them), the winner is unchanged. Completes the
    /// tournament once a single player remains. Returns the new log entry so the caller can persist it.
    /// </summary>
    public ChipGameEntry RecordGame(Tournament tournament, Guid winnerId, Guid loserId)
    {
        var detail = RequireActive(tournament);

        if (winnerId == loserId)
        {
            throw new InvalidOperationException("A game needs two different players.");
        }

        var winner = tournament.Entrants.FirstOrDefault(e => e.Id == winnerId)
            ?? throw new InvalidOperationException("The winner is not in this tournament.");
        var loser = tournament.Entrants.FirstOrDefault(e => e.Id == loserId)
            ?? throw new InvalidOperationException("The loser is not in this tournament.");

        var chips = ChipCounts(tournament);
        if (chips[winnerId] <= 0)
        {
            throw new InvalidOperationException($"{Name(winner)} has been eliminated and can't play.");
        }
        if (chips[loserId] <= 0)
        {
            throw new InvalidOperationException($"{Name(loser)} has been eliminated and can't play.");
        }

        var entry = new ChipGameEntry
        {
            ChipGameDetailId = detail.Id,
            WinnerEntrantId = winnerId,
            LoserEntrantId = loserId,
            Sequence = NextSequence(detail)
        };
        detail.Entries.Add(entry);

        if (chips[loserId] - 1 <= 0)
        {
            loser.IsEliminated = true;
        }

        if (tournament.Entrants.Count(e => !e.IsEliminated) <= 1)
        {
            tournament.Status = TournamentStatus.Completed;
        }

        return entry;
    }

    /// <summary>
    /// Per-player standings computed from the game log: active players first (most chips on top),
    /// then eliminated players in reverse elimination order (last one out finishes highest).
    /// Finishing places are locked for eliminated players and for the champion once the tournament
    /// is complete; the payout column reflects the configured 1st/2nd/3rd prizes.
    /// </summary>
    public static List<ChipStandingRow> ComputeStandings(Tournament tournament)
    {
        var detail = tournament.ChipGame;
        var entrants = tournament.Entrants;
        var total = entrants.Count;
        var start = detail?.StartingChips ?? 0;

        var chips = entrants.ToDictionary(e => e.Id, _ => start);
        var eliminationSequence = new Dictionary<Guid, int>();
        var sequence = 0;
        foreach (var entry in (detail?.Entries ?? new List<ChipGameEntry>()).OrderBy(e => e.Sequence))
        {
            if (chips.TryGetValue(entry.LoserEntrantId, out var remaining))
            {
                chips[entry.LoserEntrantId] = remaining - 1;
                if (chips[entry.LoserEntrantId] <= 0 && !eliminationSequence.ContainsKey(entry.LoserEntrantId))
                {
                    eliminationSequence[entry.LoserEntrantId] = sequence;
                }
            }
            sequence++;
        }

        // Rank eliminated players by when they went out (earliest first). The first player out
        // finishes last (place = total); the last player out finishes 2nd.
        var eliminationRank = eliminationSequence
            .OrderBy(kv => kv.Value)
            .Select((kv, index) => (kv.Key, index))
            .ToDictionary(x => x.Key, x => x.index);

        var activeCount = entrants.Count(e => chips[e.Id] > 0);
        var completed = tournament.Status == TournamentStatus.Completed;

        var rows = entrants.Select(entrant =>
        {
            var remaining = Math.Max(0, chips[entrant.Id]);
            var isEliminated = remaining <= 0;

            int? place = null;
            if (isEliminated)
            {
                place = total - eliminationRank[entrant.Id];
            }
            else if (completed && activeCount == 1)
            {
                place = 1;
            }

            var payout = place switch
            {
                1 => detail?.FirstPlacePayout ?? 0m,
                2 => detail?.SecondPlacePayout ?? 0m,
                3 => detail?.ThirdPlacePayout ?? 0m,
                _ => 0m
            };

            return new ChipStandingRow
            {
                Entrant = entrant,
                ChipsRemaining = remaining,
                IsEliminated = isEliminated,
                Place = place,
                Payout = payout
            };
        });

        return rows
            .OrderBy(r => r.IsEliminated)
            .ThenByDescending(r => r.IsEliminated ? 0 : r.ChipsRemaining)
            .ThenBy(r => r.Place ?? int.MaxValue)
            .ThenBy(r => r.Entrant.Player?.LastName)
            .ThenBy(r => r.Entrant.Player?.FirstName)
            .ToList();
    }

    /// <summary>Total prize pool: the buy-in times the number of entrants.</summary>
    public static decimal Pot(Tournament tournament) =>
        (tournament.ChipGame?.BuyInAmount ?? 0m) * tournament.Entrants.Count;

    private static Dictionary<Guid, int> ChipCounts(Tournament tournament)
    {
        var detail = tournament.ChipGame;
        var start = detail?.StartingChips ?? 0;
        var chips = tournament.Entrants.ToDictionary(e => e.Id, _ => start);
        foreach (var entry in detail?.Entries ?? new List<ChipGameEntry>())
        {
            if (chips.ContainsKey(entry.LoserEntrantId))
            {
                chips[entry.LoserEntrantId]--;
            }
        }
        return chips;
    }

    private static ChipGameDetail RequireActive(Tournament tournament)
    {
        if (tournament.ChipGame is null)
        {
            throw new InvalidOperationException("This tournament is not a chip tournament.");
        }
        if (tournament.Status == TournamentStatus.Completed)
        {
            throw new InvalidOperationException("This chip tournament has already finished.");
        }
        return tournament.ChipGame;
    }

    private static int NextSequence(ChipGameDetail detail) =>
        detail.Entries.Count == 0 ? 0 : detail.Entries.Max(e => e.Sequence) + 1;

    private static string Name(TournamentEntrant entrant) => entrant.Player?.FullName ?? "That player";
}
