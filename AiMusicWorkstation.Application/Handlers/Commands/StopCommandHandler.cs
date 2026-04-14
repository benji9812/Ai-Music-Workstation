using AiMusicWorkstation.Application.Commands.Playback;
using AiMusicWorkstation.Domain.Services;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Application.Handlers.Commands;

public class StopCommandHandler : ICommandHandler<StopCommand>
{
    private readonly IAudioPlayer _player;
    private readonly ILogger<StopCommandHandler> _logger;
    
    public StopCommandHandler(IAudioPlayer player, ILogger<StopCommandHandler> logger)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task Handle(StopCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            _player.Stop();
            _logger.LogInformation("Playback stopped");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping playback");
            throw;
        }
    }
}
