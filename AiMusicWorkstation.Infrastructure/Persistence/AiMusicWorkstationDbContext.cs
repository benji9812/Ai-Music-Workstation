using AiMusicWorkstation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiMusicWorkstation.Infrastructure.Persistence;

public class AiMusicWorkstationDbContext : DbContext
{
    public AiMusicWorkstationDbContext(DbContextOptions<AiMusicWorkstationDbContext> options)
        : base(options)
    {
    }

    public DbSet<InviteToken> InviteTokens => Set<InviteToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InviteToken>(entity =>
        {
            entity.HasKey(token => token.Id);
            entity.Property(token => token.Token)
                .HasMaxLength(128)
                .IsRequired();
            entity.HasIndex(token => token.Token)
                .IsUnique();
            entity.Property(token => token.IsActive)
                .HasDefaultValue(true);
            entity.Property(token => token.CreatedAt)
                .HasDefaultValueSql("now()");
        });
    }
}
