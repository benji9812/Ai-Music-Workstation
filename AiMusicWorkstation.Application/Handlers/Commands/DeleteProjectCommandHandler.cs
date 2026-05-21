using AiMusicWorkstation.Application.Commands.Library;
using AiMusicWorkstation.Domain.Repositories;
using AiMusicWorkstation.Domain.Services;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Application.Handlers.Commands;

public class DeleteProjectCommandHandler : ICommandHandler<DeleteProjectCommand>
{
    private readonly ILibraryRepository _repository;
    private readonly IProjectFileManager _fileManager;
    private readonly ILogger<DeleteProjectCommandHandler> _logger;
    
    public DeleteProjectCommandHandler(
        ILibraryRepository repository,
        IProjectFileManager fileManager,
        ILogger<DeleteProjectCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task Handle(DeleteProjectCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(command.ProjectId))
                throw new ArgumentException("ProjectId is required", nameof(command.ProjectId));
            
            var project = await _repository.GetByIdAsync(command.ProjectId, cancellationToken);
            if (project == null)
            {
                _logger.LogWarning("Project not found for deletion: {ProjectId}", command.ProjectId);
                return;
            }

            await _repository.DeleteAsync(command.ProjectId, cancellationToken);
            await _repository.SaveAsync(cancellationToken);
            if (command.DeleteFiles)
            {
                await _fileManager.DeleteProjectFilesAsync(project, cancellationToken);
            }
            _logger.LogInformation("Project deleted: {ProjectId}", command.ProjectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project {ProjectId}", command.ProjectId);
            throw;
        }
    }
}
