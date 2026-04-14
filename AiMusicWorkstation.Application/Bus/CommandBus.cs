using AiMusicWorkstation.Application.Commands;
using AiMusicWorkstation.Application.Handlers.Commands;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Application.Bus;

public class CommandBus : ICommandBus
{
    private readonly IServiceProvider _services;
    private readonly ILogger<CommandBus> _logger;
    
    public CommandBus(IServiceProvider services, ILogger<CommandBus> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task Execute<TCommand>(TCommand command, CancellationToken cancellationToken = default) 
        where TCommand : class
    {
        try
        {
            var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
            dynamic handler = _services.GetService(handlerType) 
                ?? throw new InvalidOperationException($"No handler registered for {command.GetType().Name}");
            
            _logger.LogInformation("Executing command: {CommandType}", command.GetType().Name);
            await handler.Handle((dynamic)command, cancellationToken);
            _logger.LogInformation("Command executed successfully: {CommandType}", command.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command: {CommandType}", command.GetType().Name);
            throw;
        }
    }
}
