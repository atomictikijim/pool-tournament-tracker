using Microsoft.EntityFrameworkCore;
using PoolTournamentManager.Core.Entities;
using PoolTournamentManager.Core.Interfaces;

namespace PoolTournamentManager.Data.Persistence;

public class TournamentRepository : ITournamentRepository
{
    private readonly PoolTournamentDbContext _dbContext;

    public TournamentRepository(PoolTournamentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Tournament>> GetAllAsync()
    {
        return await _dbContext.Tournaments
            .OrderByDescending(t => t.Id)
            .ToListAsync();
    }

    public async Task<Tournament?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Tournaments
            .Include(t => t.Entrants).ThenInclude(e => e.Player)
            .Include(t => t.Tables)
            .Include(t => t.Matches).ThenInclude(m => m.Player1Entrant).ThenInclude(e => e!.Player)
            .Include(t => t.Matches).ThenInclude(m => m.Player2Entrant).ThenInclude(e => e!.Player)
            .Include(t => t.Matches).ThenInclude(m => m.Table)
            .Include(t => t.Bracket).ThenInclude(b => b!.Nodes).ThenInclude(n => n.Match)
            .Include(t => t.RingGame).ThenInclude(r => r!.LedgerEntries).ThenInclude(l => l.Entrant).ThenInclude(e => e!.Player)
            .Include(t => t.ChipGame).ThenInclude(c => c!.Entries)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task AddAsync(Tournament tournament)
    {
        _dbContext.Tournaments.Add(tournament);
        await _dbContext.SaveChangesAsync();
    }

    public void TrackNew(object entity)
    {
        _dbContext.Add(entity);
    }

    public void TrackRemoved(object entity)
    {
        // Entry(...).State = Deleted marks only this entity, unlike Remove(), which walks the
        // whole reachable navigation graph and re-attaches everything on it (Match.Player1Entrant
        // .Player etc.) - GetByIdAsync's multiple Include paths onto the same entities (Matches
        // directly and via Bracket.Nodes.Match) mean that walk can visit an already-tracked entity
        // through two different paths and throw a duplicate-identity error.
        _dbContext.Entry(entity).State = EntityState.Deleted;
    }

    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}
