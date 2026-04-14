using System.Threading;
using System.Threading.Tasks;
using AiMusicWorkstation.Application.Commands;

namespace AiMusicWorkstation.Application.Handlers.Commands;

public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    Task Handle(TCommand command, CancellationToken cancellationToken = default);
}
