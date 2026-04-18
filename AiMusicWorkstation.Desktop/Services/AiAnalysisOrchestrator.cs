using AiMusicWorkstation.Shared.Models;

namespace AiMusicWorkstation.Desktop.Services;

public class AiAnalysisOrchestrator
{
    private readonly PythonBridge _pythonBridge;
    private readonly AnalysisResultParser _parser;

    public AiAnalysisOrchestrator(PythonBridge pythonBridge, AnalysisResultParser parser)
    {
        _pythonBridge = pythonBridge ?? throw new ArgumentNullException(nameof(pythonBridge));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
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
}
