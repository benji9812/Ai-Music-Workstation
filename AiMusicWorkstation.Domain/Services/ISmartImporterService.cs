namespace AiMusicWorkstation.Domain.Services;

public interface ISmartImporterService
{
    Task<(string FilePath, string Title, string Artist)> DownloadSongAsync(
        string url,
        IProgress<string> progress,
        CancellationToken cancellationToken = default);
    
    Task<(string Genre, string? Artist)> GetOfficialMetadataAsync(
        string trackId,
        CancellationToken cancellationToken = default);
}
