using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Domain.Services;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Infrastructure.Services;

public class ProjectFileManager : IProjectFileManager
{
    private readonly ILogger<ProjectFileManager> _logger;

    public ProjectFileManager(ILogger<ProjectFileManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task DeleteProjectFilesAsync(SongProject project, CancellationToken cancellationToken = default)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));

        DeletePath(project.StemsPath);
        DeletePath(project.OriginalPath);

        return Task.CompletedTask;
    }

    private void DeletePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogInformation("Deleted file {Path}", path);
                return;
            }

            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
                _logger.LogInformation("Deleted directory {Path}", path);
                return;
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
                _logger.LogInformation("Deleted directory {Directory}", directory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete project path {Path}", path);
        }
    }
}
