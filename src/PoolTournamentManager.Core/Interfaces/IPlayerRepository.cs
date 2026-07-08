using PoolTournamentManager.Core.Entities;

namespace PoolTournamentManager.Core.Interfaces;

public interface IPlayerRepository
{
    Task<List<Player>> GetAllAsync();
    Task<Player?> GetByIdAsync(Guid id);
    Task AddAsync(Player player);
    Task UpdateAsync(Player player);
    Task DeleteAsync(Player player);

    /// <summary>
    /// True if the player is entered in any tournament. Deleting such a player would violate the
    /// entrant foreign key (configured <c>DeleteBehavior.Restrict</c>), so callers block instead.
    /// </summary>
    Task<bool> IsReferencedAsync(Guid playerId);
}
