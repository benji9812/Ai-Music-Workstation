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
