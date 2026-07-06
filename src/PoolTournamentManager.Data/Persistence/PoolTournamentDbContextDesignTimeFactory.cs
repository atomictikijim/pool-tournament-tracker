using Microsoft.EntityFrameworkCore.Design;

namespace PoolTournamentManager.Data.Persistence;

public class PoolTournamentDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PoolTournamentDbContext>
{
    public PoolTournamentDbContext CreateDbContext(string[] args)
    {
        return PoolTournamentDbContextFactory.Create(PoolTournamentDbContextFactory.GetDefaultDatabasePath());
    }
}
