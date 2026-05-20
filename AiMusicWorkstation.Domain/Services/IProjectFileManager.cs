using AiMusicWorkstation.Domain.Entities;

namespace AiMusicWorkstation.Domain.Services;

public interface IProjectFileManager
{
    Task DeleteProjectFilesAsync(SongProject project, CancellationToken cancellationToken = default);
}
