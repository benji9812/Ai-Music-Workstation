using AiMusicWorkstation.Application.Handlers.Queries;
using AiMusicWorkstation.Application.Queries.Library;
using AiMusicWorkstation.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AiMusicWorkstation.Tests.Application.Handlers;

public class FilterProjectsQueryHandlerTests
{
    private readonly Mock<ILibraryRepository> _repository = new();
    private readonly FilterProjectsQueryHandler _handler;

    public FilterProjectsQueryHandlerTests()
    {
        _handler = new FilterProjectsQueryHandler(
            _repository.Object,
            new Mock<ILogger<FilterProjectsQueryHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ValidQuery_PassesUserIdAndCancellationTokenToRepository()
    {
        var userId = Guid.NewGuid();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var query = new FilterProjectsQuery { UserId = userId };

        _repository
            .Setup(repository => repository.GetAllAsync(userId, cancellationToken))
            .ReturnsAsync([]);

        await _handler.Handle(query, cancellationToken);

        _repository.Verify(repository => repository.GetAllAsync(userId, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyUserId_ThrowsAndDoesNotCallRepository()
    {
        var query = new FilterProjectsQuery { UserId = Guid.Empty };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(query));

        Assert.Equal("UserId", exception.ParamName);
        _repository.VerifyNoOtherCalls();
    }
}
