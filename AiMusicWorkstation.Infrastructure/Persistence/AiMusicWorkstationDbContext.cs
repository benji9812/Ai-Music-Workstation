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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SongProject>()
            .HasOne(p => p.Group)
            .WithMany(g => g.Songs)
            .HasForeignKey(p => p.GroupId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
