using AiMusicWorkstation.Application.Queries;

namespace AiMusicWorkstation.Application.Queries.Playback;

public class GetPlaybackStateQuery : IQuery<PlaybackStateDto> { }

public class PlaybackStateDto
{
    public bool IsPlaying { get; set; }
    public TimeSpan CurrentTime { get; set; }
    public TimeSpan TotalTime { get; set; }
    public int Bpm { get; set; }
    public string CurrentKey { get; set; } = string.Empty;
}
