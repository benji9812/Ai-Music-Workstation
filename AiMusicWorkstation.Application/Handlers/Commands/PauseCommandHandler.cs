using AiMusicWorkstation.Application.Commands.Playback;
using AiMusicWorkstation.Domain.Services;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Application.Handlers.Commands;

public class PauseCommandHandler : ICommandHandler<PauseCommand>
{
    private readonly IAudioPlayer _player;
    private readonly ILogger<PauseCommandHandler> _logger;
    
    public PauseCommandHandler(IAudioPlayer player, ILogger<PauseCommandHandler> logger)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public Task Handle(PauseCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_player.IsPlaying)
            {
                _logger.LogWarning("Attempt to pause while not playing");
                return Task.CompletedTask;
            }
            
            _player.Pause();
            _logger.LogInformation("Playback paused");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing playback");
            throw;
        }
    }
}
