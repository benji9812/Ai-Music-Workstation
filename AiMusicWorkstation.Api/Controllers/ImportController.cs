using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Domain.Repositories;
using AiMusicWorkstation.Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace AiMusicWorkstation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    private readonly SmartImporter _smartImporter;
    private readonly ILibraryRepository _repository;
    private readonly PythonEngineClient _pythonClient;
    private readonly ILogger<ImportController> _logger;

    public ImportController(
        SmartImporter smartImporter, 
        ILibraryRepository repository, 
        PythonEngineClient pythonClient,
        ILogger<ImportController> logger)
    {
        _smartImporter = smartImporter ?? throw new ArgumentNullException(nameof(smartImporter));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _pythonClient = pythonClient ?? throw new ArgumentNullException(nameof(pythonClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("youtube")]
    public async Task<IActionResult> ImportFromUrl([FromBody] ImportRequest request)
    {
        if (string.IsNullOrEmpty(request?.Url))
            return BadRequest(new { status = "error", message = "URL missing" });

        try
        {
            // 1. Download via SmartImporter
            var progress = new Progress<string>(msg => _logger.LogInformation("Import: {Msg}", msg));
            var downloadResult = await _smartImporter.DownloadSongAsync(request.Url, progress);

            if (!System.IO.File.Exists(downloadResult.FilePath))
                return StatusCode(500, new { status = "error", message = "Download failed." });

            // 2. Fetch extra metadata if Spotify
            string genre = "Uncategorized";
            if (!string.IsNullOrEmpty(downloadResult.SpotifyId))
            {
                var meta = await _smartImporter.GetOfficialMetadata(downloadResult.SpotifyId);
                genre = meta?.Genre ?? "Uncategorized";
            }

            // 3. Send file to Python Engine for analysis
            using var fileStream = new FileStream(downloadResult.FilePath, FileMode.Open, FileAccess.Read);
            var formFile = new FormFile(fileStream, 0, fileStream.Length, "file", Path.GetFileName(downloadResult.FilePath))
            {
                Headers = new HeaderDictionary(),
                ContentType = "audio/mpeg"
            };

            // Call Python engine
            var analysisResponseStr = await _pythonClient.AnalyzeAsync(formFile);
            
            // Cleanup local downloaded file to save space on Azure Web App
            try { fileStream.Close(); System.IO.File.Delete(downloadResult.FilePath); } catch { }
            
            // 4. Save to Database
            var project = new SongProject
            {
                Id = Guid.NewGuid().ToString(),
                Title = downloadResult.Title,
                Artist = downloadResult.Artist,
                Genre = genre,
                StemsPath = "", // This will be parsed in frontend
                Bpm = 0,
                Key = "",
                DateAdded = DateTime.UtcNow
            };
            
            await _repository.AddAsync(project);
            await _repository.SaveAsync();

            return Content(analysisResponseStr, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import url: {Url}", request.Url);
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }
}

public class ImportRequest
{
    public string Url { get; set; }
}
