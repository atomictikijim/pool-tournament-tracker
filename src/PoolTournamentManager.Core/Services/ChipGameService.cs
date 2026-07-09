using System.Text.RegularExpressions;
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

    public int MatchesWon { get; init; }
    public int MatchesPlayed { get; init; }

    /// <summary>0-100. 0 when MatchesPlayed is 0.</summary>
    public double WinPercentage { get; init; }
}

/// <summary>One table's current occupants in a chip tournament's rotation. Either seat may be
/// empty (waiting for a challenger from NextUp, or idle if no one is available yet).</summary>
public class ChipTableSeat
{
    public required Table Table { get; init; }
    public TournamentEntrant? Player1 { get; init; }
    public TournamentEntrant? Player2 { get; init; }
}

/// <summary>The full state of a chip tournament's table rotation at a point in time: who's
/// seated where, and who's waiting to be seated next, in order.</summary>
public class ChipTableBoard
{
    public required List<ChipTableSeat> Tables { get; init; }
    public required List<TournamentEntrant> NextUp { get; init; }
}

/// <summary>
/// Drives a "lives" chip tournament: every player starts with the same number of chips, and each
/// recorded game (winner stays at their table, challenger comes from the rotation) costs the
/// loser one chip - the winner's count is unchanged. A player at 0 chips is eliminated; the last
/// player still holding a chip wins. Chip counts, finishing places, and the table
/// board/rotation are all always recomputed from the game log, never stored, so any game can be
/// replayed or (in future) undone from the ledger alone.
/// </summary>
public class ChipGameService
{
    private static readonly Regex TrailingDigits = new(@"(\d+)$", RegexOptions.Compiled);

    /// <summary>
    /// Sets up a chip tournament on an already-populated tournament: gives every entrant the same
    /// starting chip count and marks the tournament InProgress. Entry fee/host cut/prize payouts
    /// are the generic Tournament fields (EntryFee etc.), already set by the caller.
    /// </summary>
    public ChipGameDetail StartChipTournament(Tournament tournament, int startingChips)
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
            StartingChips = startingChips
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
    /// Randomly shuffles the still-active entrants into rotation order (TournamentEntrant.SeedNumber),
    /// which ComputeTableBoard uses to seat them at the available tables. Allowed any time before the
    /// first table-tracked game is recorded - including to re-shuffle, or to incorporate a table added
    /// late - but not once table rotation has actually started, since that would scramble a live board.
    /// A tournament that already has legacy games recorded without a table (from before this feature
    /// existed) can still be shuffled once, so it can carry on using the table board going forward.
    /// </summary>
    public void ShuffleAndSeatPlayers(Tournament tournament)
    {
        var detail = tournament.ChipGame
            ?? throw new InvalidOperationException("This tournament is not a chip tournament.");

        if (detail.Entries.Any(e => e.TableId is not null))
        {
            throw new InvalidOperationException("Table rotation has already started - players can't be re-shuffled.");
        }

        foreach (var entrant in tournament.Entrants)
        {
            entrant.SeedNumber = null;
        }

        var active = tournament.Entrants
            .Where(e => !e.IsEliminated)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        for (var i = 0; i < active.Count; i++)
        {
            active[i].SeedNumber = i + 1;
        }
    }

    /// <summary>
    /// Records that <paramref name="winnerId"/> beat <paramref name="loserId"/> at <paramref name="tableId"/>:
    /// the loser drops a chip (and is eliminated if that empties them), the winner is unchanged and
    /// stays seated at that table. Completes the tournament once a single player remains. Returns the
    /// new log entry so the caller can persist it.
    /// </summary>
    public ChipGameEntry RecordGame(Tournament tournament, Guid tableId, Guid winnerId, Guid loserId)
    {
        var detail = RequireActive(tournament);

        if (winnerId == loserId)
        {
            throw new InvalidOperationException("A game needs two different players.");
        }

        var table = tournament.Tables.FirstOrDefault(t => t.Id == tableId && t.IsActive)
            ?? throw new InvalidOperationException("That table isn't available.");

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

        var board = ComputeTableBoard(tournament);
        var seat = board.Tables.FirstOrDefault(s => s.Table.Id == tableId);
        var seated = new HashSet<Guid>();
        if (seat?.Player1 is not null) seated.Add(seat.Player1.Id);
        if (seat?.Player2 is not null) seated.Add(seat.Player2.Id);
        if (seated.Count != 2 || !seated.Contains(winnerId) || !seated.Contains(loserId))
        {
            throw new InvalidOperationException($"{table.Label}'s seating just changed - refresh and try again.");
        }

        var entry = new ChipGameEntry
        {
            ChipGameDetailId = detail.Id,
            WinnerEntrantId = winnerId,
            LoserEntrantId = loserId,
            TableId = tableId,
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
    /// Computes the current table board: who's seated where, and who's waiting in the "Next Up"
    /// rotation, by replaying the shuffle order (SeedNumber) and the game log (Entries) from
    /// scratch. Never stored - always recomputed, same rule as ComputeStandings.
    ///
    /// Initial seating takes two entrants at a time (in shuffle order) per table, in table order.
    /// Each recorded game removes the loser from their table (the winner stays) and, if the loser
    /// still has chips, sends them to the back of the queue; any vacancy is immediately re-filled
    /// from the front of the queue, in table order. Once the queue is empty, any tables left with
    /// exactly one occupant are paired up with each other (earliest table keeps the match, later
    /// table goes idle) - without this, the board stalls near the end of a tournament whenever the
    /// table count doesn't evenly divide the number of players still standing.
    /// </summary>
    public static ChipTableBoard ComputeTableBoard(Tournament tournament)
    {
        var tables = tournament.Tables.Where(t => t.IsActive).OrderBy(TableSortKey).ToList();
        var occupants = tables.ToDictionary(t => t.Id, _ => new List<TournamentEntrant>());

        // A SeedNumber means this entrant was still active the moment ShuffleAndSeatPlayers ran,
        // so they belong in the replay even if a later (table-tracked) entry in this same replay
        // goes on to eliminate them - the entries loop below removes them from their seat at the
        // right point. Entrants with no SeedNumber are either a genuine late add after the shuffle
        // (not eliminated - falls to the back of the queue) or eliminated by a legacy pre-shuffle
        // game that never had a table (excluded entirely, same as they always were).
        var seedOrder = tournament.Entrants
            .Where(e => e.SeedNumber is not null || !e.IsEliminated)
            .OrderBy(e => e.SeedNumber ?? int.MaxValue)
            .ToList();
        var nextUp = new List<TournamentEntrant>();

        foreach (var table in tables)
        {
            var seatList = occupants[table.Id];
            while (seatList.Count < 2 && seedOrder.Count > 0)
            {
                seatList.Add(seedOrder[0]);
                seedOrder.RemoveAt(0);
            }
        }
        nextUp.AddRange(seedOrder);

        var start = tournament.ChipGame?.StartingChips ?? 0;
        var chips = tournament.Entrants.ToDictionary(e => e.Id, _ => start);

        foreach (var entry in (tournament.ChipGame?.Entries ?? new List<ChipGameEntry>()).OrderBy(e => e.Sequence))
        {
            if (chips.ContainsKey(entry.LoserEntrantId))
            {
                chips[entry.LoserEntrantId]--;
            }

            if (entry.TableId is not Guid tableId || !occupants.TryGetValue(tableId, out var seatList))
            {
                continue;
            }

            seatList.RemoveAll(e => e.Id == entry.LoserEntrantId);
            if (!seatList.Any(e => e.Id == entry.WinnerEntrantId))
            {
                var winnerEntrant = tournament.Entrants.FirstOrDefault(e => e.Id == entry.WinnerEntrantId);
                if (winnerEntrant is not null)
                {
                    seatList.Add(winnerEntrant);
                }
            }

            var loserRemaining = chips.TryGetValue(entry.LoserEntrantId, out var remaining) ? remaining : 0;
            if (loserRemaining > 0)
            {
                var loserEntrant = tournament.Entrants.FirstOrDefault(e => e.Id == entry.LoserEntrantId);
                if (loserEntrant is not null)
                {
                    nextUp.Add(loserEntrant);
                }
            }

            FillVacancies(tables, occupants, nextUp);
            ConsolidateSingles(tables, occupants, nextUp);
        }

        var seats = tables.Select(table =>
        {
            var seatList = occupants[table.Id];
            return new ChipTableSeat
            {
                Table = table,
                Player1 = seatList.Count > 0 ? seatList[0] : null,
                Player2 = seatList.Count > 1 ? seatList[1] : null
            };
        }).ToList();

        return new ChipTableBoard { Tables = seats, NextUp = nextUp };
    }

    private static void FillVacancies(
        List<Table> tables, Dictionary<Guid, List<TournamentEntrant>> occupants, List<TournamentEntrant> nextUp)
    {
        foreach (var table in tables)
        {
            var seatList = occupants[table.Id];
            while (seatList.Count < 2 && nextUp.Count > 0)
            {
                seatList.Add(nextUp[0]);
                nextUp.RemoveAt(0);
            }
        }
    }

    private static void ConsolidateSingles(
        List<Table> tables, Dictionary<Guid, List<TournamentEntrant>> occupants, List<TournamentEntrant> nextUp)
    {
        if (nextUp.Count > 0)
        {
            return;
        }

        var singles = tables.Where(t => occupants[t.Id].Count == 1).ToList();
        while (singles.Count >= 2)
        {
            var earlier = singles[0];
            var later = singles[1];
            occupants[earlier.Id].Add(occupants[later.Id][0]);
            occupants[later.Id].Clear();
            singles.RemoveRange(0, 2);
        }
    }

    /// <summary>Sort key for tables: the trailing integer in the label (tables are always
    /// machine-labelled "Table {n}" and never renamed today), falling back to an ordinal string
    /// compare if a label doesn't end in digits.</summary>
    private static (int Order, string Label) TableSortKey(Table table)
    {
        var match = TrailingDigits.Match(table.Label);
        return match.Success ? (int.Parse(match.Value), table.Label) : (int.MaxValue, table.Label);
    }

    /// <summary>
    /// Per-player standings computed from the game log: active players first (most chips on top),
    /// then eliminated players in reverse elimination order (last one out finishes highest).
    /// Finishing places are locked for eliminated players and for the champion once the tournament
    /// is complete. Payouts for each place are computed separately by PrizePayoutService.
    /// </summary>
    public static List<ChipStandingRow> ComputeStandings(Tournament tournament)
    {
        var detail = tournament.ChipGame;
        var entrants = tournament.Entrants;
        var total = entrants.Count;
        var start = detail?.StartingChips ?? 0;

        var chips = entrants.ToDictionary(e => e.Id, _ => start);
        var wins = entrants.ToDictionary(e => e.Id, _ => 0);
        var played = entrants.ToDictionary(e => e.Id, _ => 0);
        var eliminationSequence = new Dictionary<Guid, int>();
        var sequence = 0;
        foreach (var entry in (detail?.Entries ?? new List<ChipGameEntry>()).OrderBy(e => e.Sequence))
        {
            if (wins.ContainsKey(entry.WinnerEntrantId))
            {
                wins[entry.WinnerEntrantId]++;
                played[entry.WinnerEntrantId]++;
            }
            if (chips.TryGetValue(entry.LoserEntrantId, out var remaining))
            {
                chips[entry.LoserEntrantId] = remaining - 1;
                played[entry.LoserEntrantId]++;
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

            var gamesPlayed = played[entrant.Id];
            var gamesWon = wins[entrant.Id];

            return new ChipStandingRow
            {
                Entrant = entrant,
                ChipsRemaining = remaining,
                IsEliminated = isEliminated,
                Place = place,
                MatchesWon = gamesWon,
                MatchesPlayed = gamesPlayed,
                WinPercentage = gamesPlayed > 0 ? gamesWon * 100.0 / gamesPlayed : 0
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
