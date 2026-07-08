using PoolTournamentManager.Core.Entities;

namespace PoolTournamentManager.Core.Interfaces;

public interface ITeamRepository
{
    Task<List<Team>> GetAllAsync();
    Task<Team?> GetByIdAsync(Guid id);
    Task AddAsync(Team team);
    Task UpdateAsync(Team team);
    Task DeleteAsync(Team team);

    /// <summary>
    /// True if the team is entered in any tournament. Deleting such a team would violate the
    /// entrant foreign key (configured <c>DeleteBehavior.Restrict</c>), so callers block instead.
    /// </summary>
    Task<bool> IsReferencedAsync(Guid teamId);
}
