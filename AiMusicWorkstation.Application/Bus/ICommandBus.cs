namespace AiMusicWorkstation.Application.Bus;

public interface ICommandBus
{
    Task Execute<TCommand>(TCommand command, CancellationToken cancellationToken = default) 
        where TCommand : class;
}
