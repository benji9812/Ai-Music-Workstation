using AiMusicWorkstation.Domain.Repositories;
using AiMusicWorkstation.Shared.Models;

namespace AiMusicWorkstation.Desktop.Services;

/// <summary>
/// Adapter to make LibraryManager compatible with ILibraryRepository
/// </summary>
public class DefaultLibraryRepository : ILibraryRepository
{
    private readonly LibraryManager _libraryManager;
    
    public DefaultLibraryRepository(LibraryManager libraryManager)
    {
        _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
    }
    
    public async Task<List<SongProject>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_libraryManager.Projects);
    }
    
    public async Task<SongProject?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_libraryManager.Projects.FirstOrDefault(p => p.Id == id));
    }
    
    public async Task AddAsync(SongProject project, CancellationToken cancellationToken = default)
    {
        _libraryManager.AddProject(project);
        await Task.CompletedTask;
    }
    
    public async Task UpdateAsync(SongProject project, CancellationToken cancellationToken = default)
    {
        var existing = _libraryManager.Projects.FirstOrDefault(p => p.Id == project.Id);
        if (existing != null)
        {
            _libraryManager.Projects.Remove(existing);
            _libraryManager.Projects.Add(project);
            _libraryManager.SaveLibrary();
        }
        await Task.CompletedTask;
    }
    
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var project = _libraryManager.Projects.FirstOrDefault(p => p.Id == id);
        if (project != null)
        {
            _libraryManager.Projects.Remove(project);
            _libraryManager.SaveLibrary();
        }
        await Task.CompletedTask;
    }
    
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        _libraryManager.SaveLibrary();
        await Task.CompletedTask;
    }
}
