using Microsoft.EntityFrameworkCore;
using PoolTournamentManager.Core.Entities;

namespace PoolTournamentManager.Data.Persistence;

public class PoolTournamentDbContext : DbContext
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<TournamentEntrant> TournamentEntrants => Set<TournamentEntrant>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<BracketDetail> BracketDetails => Set<BracketDetail>();
    public DbSet<BracketNode> BracketNodes => Set<BracketNode>();
    public DbSet<RingGameDetail> RingGameDetails => Set<RingGameDetail>();
    public DbSet<RingLedgerEntry> RingLedgerEntries => Set<RingLedgerEntry>();
    public DbSet<ChipGameDetail> ChipGameDetails => Set<ChipGameDetail>();
    public DbSet<ChipGameEntry> ChipGameEntries => Set<ChipGameEntry>();
    public DbSet<TournamentPrizePlace> TournamentPrizePlaces => Set<TournamentPrizePlace>();

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

        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).IsRequired();

            entity.HasMany(t => t.Entrants).WithOne().HasForeignKey(e => e.TournamentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(t => t.Tables).WithOne().HasForeignKey(tb => tb.TournamentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(t => t.Matches).WithOne().HasForeignKey(m => m.TournamentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(t => t.PrizePlaces).WithOne().HasForeignKey(p => p.TournamentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(t => t.Bracket).WithOne().HasForeignKey<BracketDetail>(b => b.TournamentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(t => t.RingGame).WithOne().HasForeignKey<RingGameDetail>(r => r.TournamentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(t => t.ChipGame).WithOne().HasForeignKey<ChipGameDetail>(c => c.TournamentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TournamentEntrant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Player).WithMany().HasForeignKey(e => e.PlayerId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Team).WithMany().HasForeignKey(e => e.TeamId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).IsRequired();
        });

        modelBuilder.Entity<Table>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Label).IsRequired();
        });

        modelBuilder.Entity<Match>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasOne(m => m.Player1Entrant).WithMany().HasForeignKey(m => m.Player1EntrantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.Player2Entrant).WithMany().HasForeignKey(m => m.Player2EntrantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.Table).WithMany().HasForeignKey(m => m.TableId).OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(m => m.IsBye);
        });

        modelBuilder.Entity<BracketDetail>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.HasMany(b => b.Nodes).WithOne().HasForeignKey(n => n.BracketDetailId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BracketNode>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.HasOne(n => n.Match).WithMany().HasForeignKey(n => n.MatchId).OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(n => n.Slot1Resolved);
            entity.Ignore(n => n.Slot2Resolved);
        });

        modelBuilder.Entity<RingGameDetail>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasMany(r => r.LedgerEntries).WithOne().HasForeignKey(l => l.RingGameDetailId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RingLedgerEntry>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.HasOne(l => l.Entrant).WithMany().HasForeignKey(l => l.EntrantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChipGameDetail>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasMany(c => c.Entries).WithOne().HasForeignKey(e => e.ChipGameDetailId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChipGameEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.WinnerEntrant).WithMany().HasForeignKey(e => e.WinnerEntrantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.LoserEntrant).WithMany().HasForeignKey(e => e.LoserEntrantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TournamentPrizePlace>(entity =>
        {
            entity.HasKey(p => p.Id);
        });
    }
}
