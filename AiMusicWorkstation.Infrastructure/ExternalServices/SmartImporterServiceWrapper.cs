using AiMusicWorkstation.Domain.Services;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Infrastructure.ExternalServices;

/// Wrapper for ISmartImporterService. The concrete implementation (SmartImporter)
/// is injected at runtime from Desktop layer.

public class SmartImporterServiceWrapper : ISmartImporterService
{
    private readonly ISmartImporterService _inner;
    private readonly ILogger<SmartImporterServiceWrapper> _logger;
    
    public SmartImporterServiceWrapper(ISmartImporterService smartImporter, ILogger<SmartImporterServiceWrapper> logger)
    {
        _inner = smartImporter ?? throw new ArgumentNullException(nameof(smartImporter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<(string FilePath, string Title, string Artist)> DownloadSongAsync(
        string url,
        IProgress<string> progress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Downloading song from {Url}", url);
            var result = await _inner.DownloadSongAsync(url, progress, cancellationToken);
            _logger.LogInformation("Downloaded song: {Title} by {Artist}", result.Title, result.Artist);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading song from {Url}", url);
            throw;
        }
    }
    
    public async Task<(string Genre, string? Artist)> GetOfficialMetadataAsync(
        string trackId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting metadata for {TrackId}", trackId);
            var result = await _inner.GetOfficialMetadataAsync(trackId, cancellationToken);
            _logger.LogInformation("Retrieved metadata - Genre: {Genre}", result.Genre);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metadata for {TrackId}", trackId);
            throw;
        }
    }
}
