using AiMusicWorkstation.Domain.Services;
using AiMusicWorkstation.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Infrastructure.ExternalServices;

/// <summary>
/// Wrapper for IPythonAnalysisService. The concrete implementation (PythonBridge)
/// is injected at runtime from Desktop layer.
/// </summary>
public class PythonBridgeService : IPythonAnalysisService
{
    private readonly IPythonAnalysisService _inner;
    private readonly ILogger<PythonBridgeService> _logger;

    public PythonBridgeService(IPythonAnalysisService pythonAnalysis, ILogger<PythonBridgeService> logger)
    {
        _inner = pythonAnalysis ?? throw new ArgumentNullException(nameof(pythonAnalysis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AnalysisResult> RunAnalysisAsync(
        string filePath,
        bool useCloud = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            _logger.LogInformation("Starting analysis for {FilePath} (useCloud={UseCloud})", filePath, useCloud);
            var result = await _inner.RunAnalysisAsync(filePath, useCloud, cancellationToken);
            _logger.LogInformation("Analysis completed successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analysis failed for {FilePath}", filePath);
            throw;
        }
    }

    public async Task<AnalysisResult> ReAnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Re-analyzing {FilePath} locally", filePath);
        return await RunAnalysisAsync(filePath, useCloud: false, cancellationToken);
    }

    public void Dispose()
    {
        _logger.LogInformation("Disposing PythonBridgeService");
        _inner.Dispose();
    }
}
