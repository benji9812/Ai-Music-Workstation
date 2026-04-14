using AiMusicWorkstation.Application.Handlers.Queries;
using AiMusicWorkstation.Application.Queries;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Application.Bus;

public class QueryBus : IQueryBus
{
    private readonly IServiceProvider _services;
    private readonly ILogger<QueryBus> _logger;
    
    public QueryBus(IServiceProvider services, ILogger<QueryBus> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<TResult> Execute<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        try
        {
            var handlerType = typeof(IQueryHandler<,>)
                .MakeGenericType(query.GetType(), typeof(TResult));
            
            dynamic handler = _services.GetService(handlerType) 
                ?? throw new InvalidOperationException($"No handler registered for {query.GetType().Name}");
            
            _logger.LogInformation("Executing query: {QueryType}", query.GetType().Name);
            var result = await handler.Handle((dynamic)query, cancellationToken);
            _logger.LogInformation("Query executed successfully: {QueryType}", query.GetType().Name);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query: {QueryType}", query.GetType().Name);
            throw;
        }
    }
}
