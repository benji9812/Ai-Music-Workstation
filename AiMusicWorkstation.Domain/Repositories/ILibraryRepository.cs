using AiMusicWorkstation.Domain.Entities;

namespace AiMusicWorkstation.Domain.Repositories;

public interface ILibraryRepository
{
    Task<List<SongProject>> GetAllAsync(Guid? userId = null, CancellationToken cancellationToken = default);
    Task<SongProject?> GetByIdAsync(string id, Guid? userId = null, CancellationToken cancellationToken = default);
    Task AddAsync(SongProject project, CancellationToken cancellationToken = default);
    Task UpdateAsync(SongProject project, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, Guid? userId = null, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
