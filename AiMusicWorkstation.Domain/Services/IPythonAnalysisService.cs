using AiMusicWorkstation.Shared.Models;

namespace AiMusicWorkstation.Domain.Services;

public interface IPythonAnalysisService : IDisposable
{
    Task<AnalysisResult> RunAnalysisAsync(
        string filePath,
        bool useCloud = true,
        CancellationToken cancellationToken = default);

    Task<AnalysisResult> ReAnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}

public class AnalysisResult
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public double Bpm { get; set; }
    public string Key { get; set; } = string.Empty;
    public string StemsPath { get; set; } = string.Empty;
    public List<LyricSegment> Lyrics { get; set; } = new();
    public List<ChordEvent> Chords { get; set; } = new();
}
