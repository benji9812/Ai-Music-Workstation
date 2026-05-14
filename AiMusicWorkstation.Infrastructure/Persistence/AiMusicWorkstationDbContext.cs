using Microsoft.EntityFrameworkCore;

namespace AiMusicWorkstation.Infrastructure.Persistence;

public class AiMusicWorkstationDbContext : DbContext
{
    public AiMusicWorkstationDbContext(DbContextOptions<AiMusicWorkstationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}