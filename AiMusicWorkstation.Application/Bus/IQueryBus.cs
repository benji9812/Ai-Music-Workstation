using AiMusicWorkstation.Application.Queries;

namespace AiMusicWorkstation.Application.Bus;

public interface IQueryBus
{
    Task<TResult> Execute<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}
