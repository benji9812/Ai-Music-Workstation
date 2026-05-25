using AiMusicWorkstation.Domain.Repositories;
using AiMusicWorkstation.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Infrastructure.Persistence;


/// Repository adapter for ILibraryRepository. The concrete implementation
/// is injected at runtime.

public class LibraryRepository : ILibraryRepository
{
    private readonly DbLibraryRepository _inner;
    private readonly ILogger<LibraryRepository> _logger;

    public LibraryRepository(DbLibraryRepository dbLibraryRepository, ILogger<LibraryRepository> logger)
    {
        _inner = dbLibraryRepository ?? throw new ArgumentNullException(nameof(dbLibraryRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<SongProject>> GetAllAsync(Guid? userId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Retrieving all projects from library for user {UserId}", userId);
            var result = await _inner.GetAllAsync(userId, cancellationToken);
            _logger.LogInformation("Retrieved {Count} projects from library", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all projects");
            throw;
        }
    }

    public async Task<SongProject?> GetByIdAsync(string id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Retrieving project with ID {Id} for user {UserId}", id, userId);
            var result = await _inner.GetByIdAsync(id, userId, cancellationToken);
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
            _logger.LogInformation("Adding project: {Title} for user {UserId}", project.Title, project.UserId);
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
            _logger.LogInformation("Updating project: {Title} for user {UserId}", project.Title, project.UserId);
            await _inner.UpdateAsync(project, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update project");
            throw;
        }
    }

    public async Task DeleteAsync(string id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting project: {Id} for user {UserId}", id, userId);
            await _inner.DeleteAsync(id, userId, cancellationToken);
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
