using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.Core.Services;

public class RoundRobinSchedulingService
{
    /// <summary>
    /// Schedules every round-robin round up front using the circle method: fix one entrant,
    /// rotate the rest by one position each round. An odd entrant count gets a ghost bye slot
    /// (no Match is created for whoever draws it that round) so the rotation math is always over
    /// an even-sized array; this naturally produces N rounds for odd N and N-1 for even N.
    /// Entrants must already have SeedNumber assigned (e.g. via SeedingService.AssignSeeds) -
    /// it's used only to order the schedule for readability, not to affect who plays whom.
    /// </summary>
    public void GenerateSchedule(Tournament tournament)
    {
        var orderedEntrants = tournament.Entrants
            .OrderBy(e => e.SeedNumber ?? int.MaxValue)
            .ToList();

        if (orderedEntrants.Count < 2)
        {
            throw new InvalidOperationException("Round robin requires at least 2 entrants.");
        }

        var slots = orderedEntrants.Select(e => (Guid?)e.Id).ToList();
        if (slots.Count % 2 != 0)
        {
            slots.Add(null);
        }

        var slotCount = slots.Count;
        var roundCount = slotCount - 1;

        for (var round = 1; round <= roundCount; round++)
        {
            for (var i = 0; i < slotCount / 2; i++)
            {
                var a = slots[i];
                var b = slots[slotCount - 1 - i];
                if (a is null || b is null)
                {
                    continue;
                }

                tournament.Matches.Add(new Match
                {
                    TournamentId = tournament.Id,
                    RoundNumber = round,
                    Player1EntrantId = a.Value,
                    Player2EntrantId = b.Value,
                    Status = MatchStatus.Scheduled
                });
            }

            var last = slots[slotCount - 1];
            for (var i = slotCount - 1; i > 1; i--)
            {
                slots[i] = slots[i - 1];
            }
            slots[1] = last;
        }

        tournament.Status = TournamentStatus.InProgress;
    }
}
