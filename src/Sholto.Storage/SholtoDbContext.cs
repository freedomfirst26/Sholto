using Microsoft.EntityFrameworkCore;
using Sholto.Storage.Entities;

namespace Sholto.Storage;

/// <summary>
/// Single DbContext for the Sholto library database. Owned indirectly through
/// IDbContextFactory&lt;SholtoDbContext&gt; — contexts are short-lived per
/// operation, not shared across threads.
/// </summary>
public sealed class SholtoDbContext : DbContext
{
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<BasicAnalysisRecord> BasicAnalyses => Set<BasicAnalysisRecord>();
    public DbSet<KeyAnalysisRecord> KeyAnalyses => Set<KeyAnalysisRecord>();
    public DbSet<BpmOverride> BpmOverrides => Set<BpmOverride>();
    public DbSet<GridAdjustment> GridAdjustments => Set<GridAdjustment>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TrackTag> TrackTags => Set<TrackTag>();
    public DbSet<Crate> Crates => Set<Crate>();
    public DbSet<CrateTrack> CrateTracks => Set<CrateTrack>();
    public DbSet<Marker> Markers => Set<Marker>();
    public DbSet<MarkerLink> MarkerLinks => Set<MarkerLink>();

    public SholtoDbContext(DbContextOptions<SholtoDbContext> options) : base(options) { }

    protected override void ConfigureConventions(ModelConfigurationBuilder cb)
    {
        // All Guid keys/FKs are stored as lowercase text; force EF to encode them
        // the same way so equality and FK inserts match. See LowercaseGuidConverter.
        cb.Properties<Guid>().HaveConversion<LowercaseGuidConverter>();
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Track>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.Path).IsUnique();
            e.Property(t => t.Path).IsRequired();
            e.Property(t => t.Title).IsRequired();
            e.Property(t => t.Artist).IsRequired();
        });

        mb.Entity<BasicAnalysisRecord>(e =>
        {
            e.HasKey(x => x.TrackId);
            e.HasOne(x => x.Track)
                .WithOne(t => t.BasicAnalysis)
                .HasForeignKey<BasicAnalysisRecord>(x => x.TrackId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<KeyAnalysisRecord>(e =>
        {
            e.HasKey(x => x.TrackId);
            e.HasOne(x => x.Track)
                .WithOne(t => t.KeyAnalysis)
                .HasForeignKey<KeyAnalysisRecord>(x => x.TrackId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<BpmOverride>(e =>
        {
            e.HasKey(x => x.TrackId);
            e.HasOne(x => x.Track)
                .WithOne(t => t.BpmOverride)
                .HasForeignKey<BpmOverride>(x => x.TrackId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<GridAdjustment>(e =>
        {
            e.HasKey(x => x.TrackId);
            e.HasOne(x => x.Track)
                .WithOne(t => t.GridAdjustment)
                .HasForeignKey<GridAdjustment>(x => x.TrackId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Setting>(e => e.HasKey(s => s.Key));

        mb.Entity<Tag>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(t => t.Name).IsUnique().HasDatabaseName("IX_Tags_Name");
            e.Property(t => t.Name).UseCollation("NOCASE");
        });

        mb.Entity<TrackTag>(e =>
        {
            e.HasKey(x => new { x.TrackId, x.TagId });
            e.HasOne(x => x.Track)
                .WithMany(t => t.TrackTags)
                .HasForeignKey(x => x.TrackId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Tag)
                .WithMany(t => t.TrackTags)
                .HasForeignKey(x => x.TagId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.TagId);
        });

        mb.Entity<Crate>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired().HasMaxLength(200);
            e.HasIndex(c => c.Name).IsUnique().HasDatabaseName("IX_Crates_Name");
            e.Property(c => c.Name).UseCollation("NOCASE");
        });

        mb.Entity<CrateTrack>(e =>
        {
            e.HasKey(x => new { x.CrateId, x.TrackId });
            e.HasOne(x => x.Crate)
                .WithMany(c => c.CrateTracks)
                .HasForeignKey(x => x.CrateId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Track)
                .WithMany(t => t.CrateTracks)
                .HasForeignKey(x => x.TrackId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.TrackId);
        });

        mb.Entity<Marker>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasOne(m => m.Track)
                .WithMany(t => t.Markers)
                .HasForeignKey(m => m.TrackId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(m => m.Kind).HasConversion<string>();
            e.HasIndex(m => m.TrackId);
        });

        mb.Entity<MarkerLink>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Transition).HasConversion<string>();
            // Deleting either endpoint marker removes the link. Restrict rather than
            // cascade so a marker delete can't fan out unexpectedly — the app clears
            // links explicitly. (SQLite needs the FKs spelled out to build the table.)
            e.HasOne(l => l.FromMarker)
                .WithMany()
                .HasForeignKey(l => l.FromMarkerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.ToMarker)
                .WithMany()
                .HasForeignKey(l => l.ToMarkerId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(l => l.FromMarkerId);
            e.HasIndex(l => l.ToMarkerId);
        });
    }
}
