using AiMusicWorkstation.Application.Handlers.Queries;
using AiMusicWorkstation.Application.Queries.Playback;
using AiMusicWorkstation.Domain.Services;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Application.Handlers.Queries;

public class GetPlaybackStateQueryHandler : IQueryHandler<GetPlaybackStateQuery, PlaybackStateDto>
{
    private readonly IAudioPlayer _player;
    private readonly ILogger<GetPlaybackStateQueryHandler> _logger;
    
    public GetPlaybackStateQueryHandler(IAudioPlayer player, ILogger<GetPlaybackStateQueryHandler> logger)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<PlaybackStateDto> Handle(GetPlaybackStateQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new PlaybackStateDto
            {
                IsPlaying = _player.IsPlaying,
                CurrentTime = _player.CurrentTime,
                TotalTime = _player.TotalTime,
                Bpm = 120, // TODO: Get from player
                CurrentKey = "--" // TODO: Get from player
            };
            
            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting playback state");
            throw;
        }
    }
}
