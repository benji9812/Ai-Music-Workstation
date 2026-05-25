using AiMusicWorkstation.Infrastructure.ExternalServices;
using AiMusicWorkstation.Shared.Models;
using AiMusicWorkstation.Domain.Services;

namespace AiMusicWorkstation.Desktop.Services;

public class AiAnalysisOrchestrator
{
    private readonly PythonBridge _pythonBridge;
    private readonly AnalysisResultParser _parser;
    private readonly ILyricsService _lyricsService;

    public AiAnalysisOrchestrator(PythonBridge pythonBridge, AnalysisResultParser parser, ILyricsService lyricsService)
    {
        _pythonBridge = pythonBridge ?? throw new ArgumentNullException(nameof(pythonBridge));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _lyricsService = lyricsService ?? throw new ArgumentNullException(nameof(lyricsService));
    }

    public async Task<AnalysisParseResult> RunAnalysisAsync(string filePath, bool useCloud = true)
    {
        string jsonResponse = await _pythonBridge.RunAnalysisAsync(filePath, useCloud: useCloud);
        return _parser.ParseAnalysis(jsonResponse);
    }

    public async Task<AnalysisResult?> TryReAnalyzeAsync(string filePath)
    {
        string jsonResponse = await _pythonBridge.ReAnalyzeAsync(filePath);
        return _parser.TryParseAnalysis(jsonResponse);
    }

    public async Task<AnalysisParseResult> RunReAnalyzeAsync(string filePath)
    {
        string jsonResponse = await _pythonBridge.ReAnalyzeAsync(filePath);
        return _parser.ParseAnalysis(jsonResponse);
    }

    public async Task<StructureParseResult> GetStructureAsync(string artist, string title, double duration)
    {
        string jsonResponse = await _pythonBridge.GetStructureAsync(artist, title, duration);
        return _parser.ParseStructure(jsonResponse, duration);
    }

    public async Task<LyricsResult?> GetLyricsAsync(string artist, string title, string? album = null, double? duration = null)
    {
        return await _lyricsService.GetLyricsAsync(artist, title, album, duration);
    }
}
