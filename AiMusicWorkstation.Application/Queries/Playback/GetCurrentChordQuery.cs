using AiMusicWorkstation.Application.Queries;

namespace AiMusicWorkstation.Application.Queries.Playback;

public class GetCurrentChordQuery : IQuery<ChordDto?> { }

public class ChordDto
{
    public string Name { get; set; } = string.Empty;
    public double Time { get; set; }
}
