using AiMusicWorkstation.Application.Commands.Playback;
using AiMusicWorkstation.Domain.Services;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Application.Handlers.Commands;

public class PlayCommandHandler : ICommandHandler<PlayCommand>
{
    private readonly IAudioPlayer _player;
    private readonly IMetronome _metronome;
    private readonly ILogger<PlayCommandHandler> _logger;
    
    public PlayCommandHandler(
        IAudioPlayer player,
        IMetronome metronome,
        ILogger<PlayCommandHandler> logger)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _metronome = metronome ?? throw new ArgumentNullException(nameof(metronome));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task Handle(PlayCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_player.IsPlaying)
            {
                _logger.LogWarning("Attempt to play while already playing");
                return;
            }
            
            _player.Play();
            _logger.LogInformation("Playback started");
            
            if (command.StartMetronome)
            {
                await _metronome.StartAsync(120, cancellationToken);
                _logger.LogInformation("Metronome started");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting playback");
            throw;
        }
    }
}
