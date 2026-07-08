using PoolTournamentManager.Core.Entities;

namespace PoolTournamentManager.Core.Interfaces;

public interface ITournamentRepository
{
    Task<List<Tournament>> GetAllAsync();
    Task<Tournament?> GetByIdAsync(Guid id);
    Task AddAsync(Tournament tournament);

    /// <summary>
    /// Permanently deletes a tournament and all of its owned data (entrants, tables, matches,
    /// bracket, prize places, ring/chip details). Players and Teams referenced by its entrants are
    /// NOT deleted. No-op if the id doesn't exist.
    /// </summary>
    Task DeleteAsync(Guid tournamentId);

    /// <summary>
    /// Explicitly marks an entity created by a Core service (and attached to an already-tracked
    /// aggregate's navigation collection) as newly-added, since change tracking can't infer that
    /// on its own for entities with client-generated keys reached only via graph fixup.
    /// </summary>
    void TrackNew(object entity);

    /// <summary>
    /// Explicitly marks an entity detached from an already-tracked aggregate's navigation
    /// collection/reference as removed, mirroring <see cref="TrackNew"/> for deletions - e.g.
    /// discarding a bracket/schedule so it can be regenerated with a newly-added entrant.
    /// </summary>
    void TrackRemoved(object entity);

    Task SaveChangesAsync();
}
