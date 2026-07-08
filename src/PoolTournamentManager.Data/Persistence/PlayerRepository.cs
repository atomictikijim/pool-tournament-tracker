using Microsoft.EntityFrameworkCore;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Interfaces;

namespace PoolTournamentManager.Data.Persistence;

public class PlayerRepository : IPlayerRepository
{
    private readonly PoolTournamentDbContext _dbContext;

    public PlayerRepository(PoolTournamentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Player>> GetAllAsync()
    {
        return await _dbContext.Players.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToListAsync();
    }

    public async Task<Player?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Players.FindAsync(id);
    }

    public async Task AddAsync(Player player)
    {
        _dbContext.Players.Add(player);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(Player player)
    {
        _dbContext.Players.Update(player);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(Player player)
    {
        _dbContext.Players.Remove(player);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> IsReferencedAsync(Guid playerId)
    {
        return await _dbContext.TournamentEntrants.AnyAsync(e => e.PlayerId == playerId);
    }
}
