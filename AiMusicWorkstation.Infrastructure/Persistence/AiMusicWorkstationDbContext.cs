using Microsoft.EntityFrameworkCore;
using AiMusicWorkstation.Domain.Entities;

namespace AiMusicWorkstation.Infrastructure.Persistence;

public class AiMusicWorkstationDbContext : DbContext
{
    public AiMusicWorkstationDbContext(DbContextOptions<AiMusicWorkstationDbContext> options)
        : base(options)
    {
    }

    public DbSet<SongProject> Projects { get; set; } = null!;
    public DbSet<SongGroup> SongGroups { get; set; } = null!;
    public DbSet<Profile> Profiles { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SongProject>(entity =>
        {
            entity.ToTable("songs");

            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.Title).HasColumnName("title");
            entity.Property(p => p.Artist).HasColumnName("artist");
            entity.Property(p => p.Bpm).HasColumnName("bpm");
            entity.Property(p => p.Key).HasColumnName("key");
            entity.Property(p => p.TimeSignature).HasColumnName("time_signature");
            entity.Property(p => p.Duration).HasColumnName("duration");
            entity.Property(p => p.DateAdded).HasColumnName("date_added");
            entity.Property(p => p.Genre).HasColumnName("genre");
            entity.Property(p => p.GroupName).HasColumnName("group_name");
            entity.Property(p => p.GroupId).HasColumnName("group_id");
            entity.Property(p => p.IsOfficialData).HasColumnName("is_official_data");
            entity.Property(p => p.OriginalPath).HasColumnName("original_path");
            entity.Property(p => p.Sections).HasColumnName("sections");
            entity.Property(p => p.SpotifyId).HasColumnName("spotify_id");
            entity.Property(p => p.StemsPath).HasColumnName("stems_path");
            entity.Property(p => p.UserId).HasColumnName("user_id");
            entity.Property(p => p.BpmSource).HasColumnName("BpmSource");
            entity.Property(p => p.KeySource).HasColumnName("KeySource");
            entity.Property(p => p.TimeSigSource).HasColumnName("TimeSignatureSource");
            entity.Property(p => p.SectionsSource).HasColumnName("StructureSource");

            entity.Ignore(p => p.DurationDisplay);
        });

        modelBuilder.Entity<SongGroup>(entity =>
        {
            entity.ToTable("song_groups");
            entity.Property(g => g.Id).HasColumnName("id");
            entity.Property(g => g.Name).HasColumnName("name");
            entity.Property(g => g.UserId).HasColumnName("user_id");
            entity.Property(g => g.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<SongProject>()
            .HasOne(p => p.Group)
            .WithMany(g => g.Songs)
            .HasForeignKey(p => p.GroupId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Profile>()
            .ToTable("profiles");
    }
}
