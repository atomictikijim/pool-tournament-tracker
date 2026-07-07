namespace PoolTournamentManager.Core.Entities;

/// <summary>
/// Chip-tournament state, attached 1:1 to a Tournament whose Format is ChipTournament - the
/// chip-tournament analogue of BracketDetail/RingGameDetail. Every player starts with
/// <see cref="StartingChips"/> chips (lives); each recorded game costs the loser one chip, and a
/// player at 0 chips is out. The last player still holding a chip wins. Chip counts and finishing
/// places are never stored - they are always recomputed from <see cref="Entries"/>.
///
/// Entry fee/host cut/prize-place payouts are the generic Tournament-level fields
/// (<see cref="Tournament.EntryFee"/> etc.), computed via PrizePayoutService - not stored here.
/// </summary>
public class ChipGameDetail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }

    /// <summary>Chips (lives) each player begins with.</summary>
    public int StartingChips { get; set; }

    public List<ChipGameEntry> Entries { get; set; } = new();
}
