using AiMusicWorkstation.Domain.Services;

namespace AiMusicWorkstation.Desktop.Services;

/// <summary>
/// Adapter to make SmartImporter compatible with ISmartImporterService
/// </summary>
public class SmartImporterAdapter : ISmartImporterService
{
    private readonly SmartImporter _smartImporter;
    
    public SmartImporterAdapter(SmartImporter smartImporter)
    {
        _smartImporter = smartImporter ?? throw new ArgumentNullException(nameof(smartImporter));
    }
    
    public async Task<(string FilePath, string Title, string Artist)> DownloadSongAsync(
        string url,
        IProgress<string> progress,
        CancellationToken cancellationToken = default)
    {
        var result = await _smartImporter.DownloadSongAsync(url, progress);
        return (result.FilePath, result.Title, result.Artist);
    }
    
    public async Task<(string Genre, string? Artist)> GetOfficialMetadataAsync(
        string trackId,
        CancellationToken cancellationToken = default)
    {
        var metadata = await _smartImporter.GetOfficialMetadata(trackId);
        return (metadata.Genre, null);
    }
}
