using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;
using AiMusicWorkstation.Infrastructure.Persistence;

namespace AiMusicWorkstation.Infrastructure
{
    public class AiMusicWorkstationDbContextFactory : IDesignTimeDbContextFactory<AiMusicWorkstationDbContext>
    {
        public AiMusicWorkstationDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = configuration["SUPABASE_CONNECTION_STRING"];
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Could not find a connection string named 'DefaultConnection' or 'SUPABASE_CONNECTION_STRING'.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<AiMusicWorkstationDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new AiMusicWorkstationDbContext(optionsBuilder.Options);
        }
    }
}
