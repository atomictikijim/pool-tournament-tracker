namespace PoolTournamentManager.Core.Enums;

/// <summary>
/// The two paying balls in a 9-ball ring game. Pocketing the 5 pays out but the rack continues;
/// pocketing the 9 pays out and ends the rack, rotating the break to the next player.
/// </summary>
public enum RingMoneyBall
{
    Five,
    Nine
}
