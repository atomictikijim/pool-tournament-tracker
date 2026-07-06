using Microsoft.EntityFrameworkCore;

namespace PoolTournamentManager.Data.Persistence;

public static class PoolTournamentDbContextFactory
{
    public static string GetDefaultDatabasePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directory = Path.Combine(localAppData, "PoolTournamentManager");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "tournaments.db");
    }

    public static PoolTournamentDbContext Create(string databasePath)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PoolTournamentDbContext>()
            .UseSqlite($"Data Source={databasePath}");
        return new PoolTournamentDbContext(optionsBuilder.Options);
    }
}
