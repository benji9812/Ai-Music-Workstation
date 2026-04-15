using AiMusicWorkstation.Domain.Repositories;
using AiMusicWorkstation.Shared.Models;

namespace AiMusicWorkstation.Desktop.Services;

/// <summary>
/// Adapter that implements ILibraryRepository by wrapping LibraryManager
/// </summary>
public class LibraryRepositoryAdapter : ILibraryRepository
{
    private readonly LibraryManager _libraryManager;

    public LibraryRepositoryAdapter(LibraryManager libraryManager)
    {
        _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
    }

    public async Task<List<SongProject>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_libraryManager.Projects.ToList());
    }

    public async Task<SongProject> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var project = _libraryManager.Projects.FirstOrDefault(p => p.Id == id);
        return await Task.FromResult(project);
    }

    public async Task AddAsync(SongProject project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        _libraryManager.AddProject(project);
        await Task.CompletedTask;
    }

    public async Task UpdateAsync(SongProject project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        var existing = _libraryManager.Projects.FirstOrDefault(p => p.Id == project.Id);
        if (existing != null)
        {
            var index = _libraryManager.Projects.IndexOf(existing);
            _libraryManager.Projects[index] = project;
        }
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Project ID is required", nameof(id));

        var project = _libraryManager.Projects.FirstOrDefault(p => p.Id == id);
        if (project != null)
        {
            _libraryManager.Projects.Remove(project);
        }
        await Task.CompletedTask;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        _libraryManager.SaveLibrary();
        await Task.CompletedTask;
    }
}
