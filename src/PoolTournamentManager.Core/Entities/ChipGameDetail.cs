namespace PoolTournamentManager.Core.Entities;

/// <summary>
/// Chip-tournament state, attached 1:1 to a Tournament whose Format is ChipTournament - the
/// chip-tournament analogue of BracketDetail/RingGameDetail. Every player starts with
/// <see cref="StartingChips"/> chips (lives); each recorded game costs the loser one chip, and a
/// player at 0 chips is out. The last player still holding a chip wins. Chip counts and finishing
/// places are never stored - they are always recomputed from <see cref="Entries"/>.
///
/// A dollar buy-in funds a pot (BuyInAmount * entrant count); the configured 1st/2nd/3rd payouts
/// are paid to the finishing places once they are decided.
/// </summary>
public class ChipGameDetail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }

    /// <summary>Chips (lives) each player begins with.</summary>
    public int StartingChips { get; set; }

    /// <summary>The fixed entry fee every player pays; the pot is this times the entrant count.</summary>
    public decimal BuyInAmount { get; set; }

    public decimal FirstPlacePayout { get; set; }
    public decimal SecondPlacePayout { get; set; }
    public decimal ThirdPlacePayout { get; set; }

    public List<ChipGameEntry> Entries { get; set; } = new();
}
