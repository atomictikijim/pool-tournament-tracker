using PoolTournamentManager.Core.Enums;

namespace PoolTournamentManager.Core.Entities;

/// <summary>
/// Chip-tournament state, attached 1:1 to a Tournament whose Format is ChipTournament - the
/// chip-tournament analogue of BracketDetail/RingGameDetail. Each player starts with a number of
/// chips (lives): either the flat <see cref="StartingChips"/> for everyone, or - when
/// <see cref="ChipRatingSystem"/> is set - the chips from the first matching <see cref="StartingRules"/>
/// range for their rating (falling back to <see cref="StartingChips"/> when they have no rating or
/// match no range). The chosen amount is snapshotted per entrant at create/edit time
/// (TournamentEntrant.StartingChips) so it can't drift if a player's rating is later edited.
///
/// Each recorded game costs the loser one chip, and a player at 0 chips is out; the last player
/// still holding a chip wins. Chip counts and finishing places are never stored - they are always
/// recomputed from the per-entrant starting chips, the director's per-entrant
/// TournamentEntrant.ChipAdjustment, and the game log in <see cref="Entries"/>.
///
/// Entry fee/host cut/prize-place payouts are the generic Tournament-level fields
/// (<see cref="Tournament.EntryFee"/> etc.), computed via PrizePayoutService - not stored here.
/// </summary>
public class ChipGameDetail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }

    /// <summary>Chips (lives) each player begins with when no skill-range rules apply - also the
    /// fallback for a player with no rating in <see cref="ChipRatingSystem"/> or matching no range.</summary>
    public int StartingChips { get; set; }

    /// <summary>Which rating drives the <see cref="StartingRules"/>; null = flat starting chips for
    /// everyone (the classic behavior, no rules).</summary>
    public RatingSystem? ChipRatingSystem { get; set; }

    /// <summary>Skill-range → starting-chips rules, evaluated in Sequence order (first match wins).
    /// Empty when <see cref="ChipRatingSystem"/> is null.</summary>
    public List<ChipStartingRule> StartingRules { get; set; } = new();

    public List<ChipGameEntry> Entries { get; set; } = new();
}
