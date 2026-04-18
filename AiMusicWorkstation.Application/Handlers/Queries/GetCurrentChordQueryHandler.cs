using AiMusicWorkstation.Application.Queries.Playback;
using AiMusicWorkstation.Domain.Services;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Application.Handlers.Queries;

public class GetCurrentChordQueryHandler : IQueryHandler<GetCurrentChordQuery, ChordDto?>
{
    private readonly IAudioPlayer _player;
    private readonly ILogger<GetCurrentChordQueryHandler> _logger;
    
    // TODO: Inject chord provider
    private List<ChordDto> _chords = new();
    
    public GetCurrentChordQueryHandler(IAudioPlayer player, ILogger<GetCurrentChordQueryHandler> logger)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<ChordDto?> Handle(GetCurrentChordQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            double currentTime = _player.CurrentTime.TotalSeconds;
            
            // Binary search for current chord (O(log n) instead of O(n))
            var chord = BinarySearchChord(currentTime);
            
            return chord;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current chord");
            throw;
        }
    }
    
    private ChordDto? BinarySearchChord(double currentTime)
    {
        if (_chords.Count == 0) return null;
        
        int left = 0, right = _chords.Count - 1;
        ChordDto? result = null;
        
        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            
            if (_chords[mid].Time <= currentTime)
            {
                result = _chords[mid];
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }
        
        return result;
    }
}
