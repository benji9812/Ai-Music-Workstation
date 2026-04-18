using AiMusicWorkstation.Domain.Repositories;
using AiMusicWorkstation.Shared.Models;

namespace AiMusicWorkstation.Desktop.Services;

/// <summary>
/// Adapter to make LibraryManager compatible with ILibraryRepository.
/// Note: This is a minimal implementation for DI purposes.
/// </summary>
public class DefaultLibraryRepository : ILibraryRepository
{
    private readonly LibraryManager _libraryManager;

    public DefaultLibraryRepository(LibraryManager libraryManager)
    {
        _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
    }

    public Task<List<SongProject>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Convert from LibraryManager.SongProject to Shared.Models.SongProject
        var result = _libraryManager.Projects
            .Select(p => new SongProject
            {
                Id = p.Id,
                Title = p.Title,
                Artist = p.Artist,
                Bpm = p.Bpm,
                Key = p.Key,
                Genre = p.Genre,
                StemsPath = p.StemsPath,
                OriginalPath = p.OriginalPath,
                SpotifyId = p.SpotifyId,
                Duration = p.Duration,
                GroupName = p.GroupName,
                DateAdded = p.DateAdded,
                IsOfficialData = p.IsOfficialData,
                TimeSignature = p.TimeSignature
            })
            .ToList();
        return Task.FromResult(result);
    }

    public Task<SongProject?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var project = _libraryManager.Projects.FirstOrDefault(p => p.Id == id);
        if (project == null) return Task.FromResult<SongProject?>(null);

        var result = new SongProject
        {
            Id = project.Id,
            Title = project.Title,
            Artist = project.Artist,
            Bpm = project.Bpm,
            Key = project.Key,
            Genre = project.Genre,
            StemsPath = project.StemsPath,
            OriginalPath = project.OriginalPath,
            SpotifyId = project.SpotifyId,
            Duration = project.Duration,
            GroupName = project.GroupName,
            DateAdded = project.DateAdded,
            IsOfficialData = project.IsOfficialData,
            TimeSignature = project.TimeSignature
        };
        return Task.FromResult<SongProject?>(result);
    }

    public Task AddAsync(SongProject project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        _libraryManager.AddProject(project);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(SongProject project, CancellationToken cancellationToken = default)
    {
        var existing = _libraryManager.Projects.FirstOrDefault(p => p.Id == project.Id);
        if (existing != null)
        {
            var index = _libraryManager.Projects.IndexOf(existing);
            _libraryManager.Projects[index] = project;
            _libraryManager.SaveLibrary();
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var project = _libraryManager.Projects.FirstOrDefault(p => p.Id == id);
        if (project != null)
        {
            _libraryManager.Projects.Remove(project);
            _libraryManager.SaveLibrary();
        }
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        _libraryManager.SaveLibrary();
        return Task.CompletedTask;
    }
}
