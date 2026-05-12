using AiMusicWorkstation.Domain.Repositories;
using AiMusicWorkstation.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Infrastructure.Persistence;


/// Repository adapter for ILibraryRepository. The concrete implementation
/// is injected at runtime.

public class LibraryRepository : ILibraryRepository
{
    private readonly ILibraryRepository _inner;
    private readonly ILogger<LibraryRepository> _logger;

    public LibraryRepository(ILibraryRepository libraryRepository, ILogger<LibraryRepository> logger)
    {
        _inner = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<SongProject>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Retrieving all projects from library");
            var result = await _inner.GetAllAsync(cancellationToken);
            _logger.LogInformation("Retrieved {Count} projects from library", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all projects");
            throw;
        }
    }

    public async Task<SongProject?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Retrieving project with ID {Id}", id);
            var result = await _inner.GetByIdAsync(id, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get project {Id}", id);
            throw;
        }
    }

    public async Task AddAsync(SongProject project, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Adding project: {Title}", project.Title);
            await _inner.AddAsync(project, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add project");
            throw;
        }
    }

    public async Task UpdateAsync(SongProject project, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating project: {Title}", project.Title);
            await _inner.UpdateAsync(project, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update project");
            throw;
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting project: {Id}", id);
            await _inner.DeleteAsync(id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete project {Id}", id);
            throw;
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Saving library");
            await _inner.SaveAsync(cancellationToken);
            _logger.LogInformation("Library saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save library");
            throw;
        }
    }
}
