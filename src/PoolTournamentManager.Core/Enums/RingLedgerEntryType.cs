namespace PoolTournamentManager.Core.Enums;

public enum RingLedgerEntryType
{
    /// <summary>The entry fee a player paid to join the ring (money into the pot).</summary>
    BuyIn,

    /// <summary>A money-ball payout collected by the shooter (money out of the pot to the player).</summary>
    MoneyBall,

    /// <summary>A player leaving the ring; a marker recording their realized net at that moment.</summary>
    CashOut
}
