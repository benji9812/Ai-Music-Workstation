using AiMusicWorkstation.Domain.Services;
using AiMusicWorkstation.Shared.Models;

namespace AiMusicWorkstation.Desktop.Services;

/// <summary>
/// Adapter to make PythonBridge compatible with IPythonAnalysisService
/// </summary>
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
        return await _pythonBridge.RunAnalysisAsync(filePath, useCloud);
    }
    
    public async Task<AnalysisResult> ReAnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return await _pythonBridge.RunAnalysisAsync(filePath, useCloud: false);
    }
    
    public void Dispose() => _pythonBridge.Dispose();
}
