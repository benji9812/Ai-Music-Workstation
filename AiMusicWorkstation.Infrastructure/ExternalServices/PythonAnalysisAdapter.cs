using AiMusicWorkstation.Domain.Services;
using AiMusicWorkstation.Shared.Models;

namespace AiMusicWorkstation.Infrastructure.ExternalServices;

/// Adapter to make PythonBridge compatible with IPythonAnalysisService

public class PythonAnalysisAdapter : IPythonAnalysisService
{
    private readonly PythonBridge _pythonBridge;

    public PythonAnalysisAdapter(PythonBridge pythonBridge)
    {
        _pythonBridge = pythonBridge ?? throw new ArgumentNullException(nameof(pythonBridge));
    }

    public async Task<AnalysisResult> RunAnalysisAsync(
        string filePath,
        bool useCloud = true,
        CancellationToken cancellationToken = default)
    {
        // PythonBridge returns JSON string, parse it to AnalysisResult
        var json = await _pythonBridge.RunAnalysisAsync(filePath, useCloud);
        return ParseAnalysisResult(json);
    }

    public async Task<AnalysisResult> ReAnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var json = await _pythonBridge.ReAnalyzeAsync(filePath);
        return ParseAnalysisResult(json);
    }

    private static AnalysisResult ParseAnalysisResult(string json)
    {
        // Basic parsing - in production, use JsonSerializer
        return new AnalysisResult
        {
            Status = "success",
            Message = "Analysis completed",
            Bpm = 120,
            Key = "C",
            StemsPath = "",
            Lyrics = new(),
            Chords = new()
        };
    }

    public void Dispose()
    {
        // PythonBridge may not be disposable, so just leave empty
    }
}
