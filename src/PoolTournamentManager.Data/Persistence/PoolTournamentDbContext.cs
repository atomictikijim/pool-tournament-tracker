using Microsoft.EntityFrameworkCore;
using PoolTournamentManager.Core.Entities;

namespace PoolTournamentManager.Data.Persistence;

public class PoolTournamentDbContext : DbContext
{
    public DbSet<Player> Players => Set<Player>();

    public PoolTournamentDbContext(DbContextOptions<PoolTournamentDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.FirstName).IsRequired();
            entity.Property(p => p.LastName).IsRequired();
        });
    }
}
