using AiMusicWorkstation.Infrastructure.ExternalServices;
using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AiMusicWorkstation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    private readonly PythonEngineClient _pythonClient;
    private readonly ILibraryRepository _repository;
    private readonly ILogger<ImportController> _logger;

    public ImportController(
        PythonEngineClient pythonClient,
        ILibraryRepository repository,
        ILogger<ImportController> logger)
    {
        _pythonClient = pythonClient ?? throw new ArgumentNullException(nameof(pythonClient));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("youtube")]
    public async Task<IActionResult> ImportFromUrl([FromBody] ImportRequest request)
    {
        if (string.IsNullOrEmpty(request?.Url))
            return BadRequest(new { status = "error", message = "URL missing" });

        try
        {
            _logger.LogInformation("ImportFromUrl: delegating to Python Engine for {Url}", request.Url);
            
            // Delegate everything to Python Engine (which has yt-dlp installed)
            var resultJson = await _pythonClient.ImportUrlAsync(request.Url);
            
            try
            {
                using var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "success")
                {
                    string title = root.TryGetProperty("title", out var titleProp) && titleProp.ValueKind != JsonValueKind.Null ? titleProp.GetString() : "Unknown Track";
                    string artist = root.TryGetProperty("artist", out var artistProp) && artistProp.ValueKind != JsonValueKind.Null ? artistProp.GetString() : "Unknown Artist";
                    double bpm = root.TryGetProperty("bpm", out var bpmProp) ? bpmProp.GetDouble() : 120.0;
                    string key = root.TryGetProperty("key", out var keyProp) && keyProp.ValueKind != JsonValueKind.Null ? keyProp.GetString() : "C";
                    string stemsPath = root.TryGetProperty("stems_path", out var stemsProp) && stemsProp.ValueKind != JsonValueKind.Null ? stemsProp.GetString() : "";
                    int timeSig = root.TryGetProperty("time_signature", out var timeSigProp) ? timeSigProp.GetInt32() : 4;
                    double durationSeconds = root.TryGetProperty("duration_seconds", out var durationProp) ? durationProp.GetDouble() : 180.0;

                    var project = new SongProject
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = title,
                        Artist = artist,
                        Bpm = bpm,
                        Key = key,
                        StemsPath = stemsPath,
                        OriginalPath = "",
                        Duration = TimeSpan.FromSeconds(durationSeconds),
                        TimeSignature = timeSig,
                        Genre = "Uncategorized",
                        DateAdded = DateTime.Now,
                        KeySource = KeySource.Generated
                    };

                    await _repository.AddAsync(project);
                    await _repository.SaveAsync();
                    
                    _logger.LogInformation("Saved imported song project to database: {Title} ({Id})", project.Title, project.Id);
                }
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Failed to parse Python response or save SongProject to database");
            }
            
            return Content(resultJson, "application/json");
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
