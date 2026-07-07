using PoolTournamentManager.Core.Entities;

namespace PoolTournamentManager.Core.Interfaces;

public interface ITeamRepository
{
    Task<List<Team>> GetAllAsync();
    Task<Team?> GetByIdAsync(Guid id);
    Task AddAsync(Team team);
    Task UpdateAsync(Team team);
}
