using Microsoft.EntityFrameworkCore;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Interfaces;

namespace PoolTournamentManager.Data.Persistence;

public class TeamRepository : ITeamRepository
{
    private readonly PoolTournamentDbContext _dbContext;

    public TeamRepository(PoolTournamentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Team>> GetAllAsync()
    {
        return await _dbContext.Teams.OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<Team?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Teams.FindAsync(id);
    }

    public async Task AddAsync(Team team)
    {
        _dbContext.Teams.Add(team);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(Team team)
    {
        _dbContext.Teams.Update(team);
        await _dbContext.SaveChangesAsync();
    }
}
