using System;
using System.Threading.Tasks;

namespace AiMusicWorkstation.Domain.Services;

public enum LyricsSource
{
    LRCLib,
    Genius
}

public class LyricsResult
{
    public string PlainLyrics { get; set; } = string.Empty;
    public string SyncedLyrics { get; set; } // Nullable, only available from LRCLib
    public LyricsSource Source { get; set; }
}

public interface ILyricsService
{
    Task<LyricsResult?> GetLyricsAsync(string artist, string title, string? album = null, double? durationSeconds = null);
}
