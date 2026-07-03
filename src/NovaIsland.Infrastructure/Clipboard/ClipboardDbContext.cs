using Microsoft.EntityFrameworkCore;
using NovaIsland.Domain.Clipboard;

namespace NovaIsland.Infrastructure.Clipboard;

public class ClipboardDbContext : DbContext
{
    public DbSet<ClipboardEntry> Entries { get; set; } = null!;

    public ClipboardDbContext() { }

    public ClipboardDbContext(DbContextOptions<ClipboardDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // SQLCipher configuration via e_sqlcipher bundle
            // For development, we use a static key. In production, this would be tied to DPAPI.
            var connectionString = "Data Source=clipboard.db;Password=NovaIslandSuperSecretKey;";
            optionsBuilder.UseSqlite(connectionString);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClipboardEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.Timestamp).IsRequired();
        });
    }
}
