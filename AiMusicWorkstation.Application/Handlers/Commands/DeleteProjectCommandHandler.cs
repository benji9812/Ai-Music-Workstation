using AiMusicWorkstation.Application.Commands.Library;
using AiMusicWorkstation.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Application.Handlers.Commands;

public class DeleteProjectCommandHandler : ICommandHandler<DeleteProjectCommand>
{
    private readonly ILibraryRepository _repository;
    private readonly ILogger<DeleteProjectCommandHandler> _logger;
    
    public DeleteProjectCommandHandler(
        ILibraryRepository repository,
        ILogger<DeleteProjectCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task Handle(DeleteProjectCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(command.ProjectId))
                throw new ArgumentException("ProjectId is required", nameof(command.ProjectId));
            
            await _repository.DeleteAsync(command.ProjectId, cancellationToken);
            await _repository.SaveAsync(cancellationToken);
            _logger.LogInformation("Project deleted: {ProjectId}", command.ProjectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project {ProjectId}", command.ProjectId);
            throw;
        }
    }
}
