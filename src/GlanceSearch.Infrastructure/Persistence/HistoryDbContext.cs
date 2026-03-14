using Microsoft.EntityFrameworkCore;

namespace GlanceSearch.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the GlanceSearch history database (SQLite).
/// </summary>
public class HistoryDbContext : DbContext
{
    private readonly string _dbPath;

    public DbSet<CaptureHistoryEntity> CaptureHistory { get; set; } = null!;

    public HistoryDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CaptureHistoryEntity>();

        entity.ToTable("CaptureHistory");

        entity.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("IX_CaptureHistory_CreatedAt");

        entity.HasIndex(e => e.IsPinned)
            .HasDatabaseName("IX_CaptureHistory_IsPinned");

        entity.HasIndex(e => e.SourceProcessName)
            .HasDatabaseName("IX_CaptureHistory_SourceProcess");
    }
}
