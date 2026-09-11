using AiMusicWorkstation.Application.Commands.Library;
using AiMusicWorkstation.Application.Handlers.Commands;
using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Domain.Repositories;
using AiMusicWorkstation.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AiMusicWorkstation.Tests.Application.Handlers;

public class DeleteProjectCommandHandlerTests
{
    private readonly Mock<ILibraryRepository> _repository = new();
    private readonly Mock<IProjectFileManager> _fileManager = new();
    private readonly DeleteProjectCommandHandler _handler;

    public DeleteProjectCommandHandlerTests()
    {
        _handler = new DeleteProjectCommandHandler(
            _repository.Object,
            _fileManager.Object,
            new Mock<ILogger<DeleteProjectCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_PassesUserIdAndCancellationTokenToRepository()
    {
        var userId = Guid.NewGuid();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var command = new DeleteProjectCommand { ProjectId = "project-1", UserId = userId };
        var project = new SongProject { Id = command.ProjectId };

        _repository
            .Setup(repository => repository.GetByIdAsync(command.ProjectId, userId, cancellationToken))
            .ReturnsAsync(project);

        await _handler.Handle(command, cancellationToken);

        _repository.Verify(repository => repository.GetByIdAsync(command.ProjectId, userId, cancellationToken), Times.Once);
        _repository.Verify(repository => repository.DeleteAsync(command.ProjectId, userId, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyUserId_ThrowsAndDoesNotCallRepository()
    {
        var command = new DeleteProjectCommand { ProjectId = "project-1", UserId = Guid.Empty };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command));

        Assert.Equal("UserId", exception.ParamName);
        _repository.VerifyNoOtherCalls();
    }
}
