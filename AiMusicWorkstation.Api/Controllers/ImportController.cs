using AiMusicWorkstation.Infrastructure.ExternalServices;
using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net.Http;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace AiMusicWorkstation.Api.Controllers;

[Authorize]
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

    private Guid? GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var userId))
        {
            return userId;
        }
        return null;
    }

    [HttpPost("youtube")]
    public async Task<IActionResult> ImportFromUrl([FromBody] ImportRequest request)
    {
        if (string.IsNullOrEmpty(request?.Url))
            return BadRequest(new { status = "error", message = "URL missing" });

        var userId = GetUserId();
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
                    string title = root.TryGetProperty("title", out var titleProp) && titleProp.ValueKind != JsonValueKind.Null ? titleProp.GetString() ?? "Unknown Track" : "Unknown Track";
                    string artist = root.TryGetProperty("artist", out var artistProp) && artistProp.ValueKind != JsonValueKind.Null ? artistProp.GetString() ?? "Unknown Artist" : "Unknown Artist";
                    double bpm = root.TryGetProperty("bpm", out var bpmProp) ? bpmProp.GetDouble() : 120.0;
                    string key = root.TryGetProperty("key", out var keyProp) && keyProp.ValueKind != JsonValueKind.Null ? keyProp.GetString() ?? "C" : "C";
                    string stemsPath = root.TryGetProperty("stems_path", out var stemsProp) && stemsProp.ValueKind != JsonValueKind.Null ? stemsProp.GetString() ?? "" : "";
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
                        BpmSource = DataSource.Analysis,
                        KeySource = DataSource.Analysis,
                        TimeSigSource = DataSource.Analysis,
                        UserId = userId
                    };

                    await _repository.AddAsync(project);
                    await _repository.SaveAsync();

                    _logger.LogInformation("Saved imported song project to database: {Title} ({Id}) for user {UserId}", project.Title, project.Id, userId);
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
    [HttpPost("start")]
    public async Task<IActionResult> StartImportAsync([FromBody] ImportRequest request)
    {
        if (string.IsNullOrEmpty(request?.Url))
            return BadRequest(new { status = "error", message = "URL missing" });
        try
        {
            var resultJson = await _pythonClient.StartImportJobAsync(request.Url);
            return Content(resultJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start async import for {Url}", request.Url);
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }

    [HttpGet("status/{jobId}")]
    public async Task<IActionResult> GetImportStatus(string jobId)
    {
        var userId = GetUserId();
        try
        {
            var resultJson = await _pythonClient.GetJobStatusAsync(jobId);
            JsonObject? jobStatus;
            try
            {
                jobStatus = JsonNode.Parse(resultJson) as JsonObject;
            }
            catch (JsonException)
            {
                return Content(resultJson, "application/json");
            }

            if (ReadString(jobStatus, "status") != "done")
                return Content(resultJson, "application/json");

            var jobType = ReadString(jobStatus, "job_type");
            if (jobType is not ("import" or "quick_import"))
                return Content(resultJson, "application/json");

            if (jobStatus?["result"] is not JsonObject result)
                return InvalidImportResult(jobId);

            var title = ReadString(result, "title");
            var stemsPath = ReadString(result, "stems_path");
            var originalPath = ReadString(result, "original_path");
            if (string.IsNullOrWhiteSpace(title) ||
                (string.IsNullOrWhiteSpace(stemsPath) && string.IsNullOrWhiteSpace(originalPath)))
            {
                return InvalidImportResult(jobId);
            }

            SongProject? persistedProject = null;
            try
            {
                persistedProject = await _repository.GetByImportJobIdAsync(jobId, userId);
                if (persistedProject == null)
                {
                    persistedProject = new SongProject
                    {
                        Id = Guid.NewGuid().ToString(),
                        ImportJobId = jobId,
                        Title = title,
                        Artist = ReadString(result, "artist", "Unknown Artist"),
                        Bpm = ReadDouble(result, "bpm", 120.0),
                        Key = ReadString(result, "key", "C"),
                        StemsPath = stemsPath,
                        OriginalPath = originalPath,
                        Duration = TimeSpan.FromSeconds(ReadDouble(result, "duration_seconds", 180.0)),
                        TimeSignature = ReadInt32(result, "time_signature", 4),
                        Genre = "Uncategorized",
                        DateAdded = DateTime.UtcNow,
                        BpmSource = DataSource.Analysis,
                        KeySource = DataSource.Analysis,
                        TimeSigSource = DataSource.Analysis,
                        UserId = userId
                    };

                    await _repository.AddAsync(persistedProject);
                    await _repository.SaveAsync();
                    _logger.LogInformation(
                        "Saved completed import job as project {ProjectId} for user {UserId}",
                        persistedProject.Id, userId);
                }
            }
            catch (Exception dbEx)
            {
                persistedProject = null;
                try
                {
                    persistedProject = await _repository.GetByImportJobIdAsync(jobId, userId);
                }
                catch (Exception recoveryEx)
                {
                    _logger.LogWarning(recoveryEx, "Failed to recover import job after persistence error");
                }

                if (persistedProject == null)
                {
                    _logger.LogError(dbEx, "Failed to persist completed import job");
                    return StatusCode(500, new
                    {
                        status = "error",
                        error_code = "persistence_failed",
                        message = "Job completed, but the imported song could not be saved.",
                        job_id = jobId
                    });
                }

                _logger.LogInformation(
                    "Recovered project {ProjectId} after concurrent persistence of import job",
                    persistedProject.Id);
            }

            result["id"] = persistedProject.Id;
            return Content(jobStatus.ToJsonString(), "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status for import job");
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }

    private IActionResult InvalidImportResult(string jobId)
    {
        return UnprocessableEntity(new
        {
            status = "error",
            error_code = "invalid_import_result",
            message = "Completed import result is missing a title or audio path.",
            job_id = jobId
        });
    }

    private static string ReadString(JsonObject? source, string propertyName, string fallback = "")
    {
        return source?[propertyName] is JsonValue value && value.TryGetValue<string>(out var result) &&
               !string.IsNullOrWhiteSpace(result)
            ? result
            : fallback;
    }

    private static double ReadDouble(JsonObject source, string propertyName, double fallback)
    {
        return source[propertyName] is JsonValue value && value.TryGetValue<double>(out var result)
            ? result
            : fallback;
    }

    private static int ReadInt32(JsonObject source, string propertyName, int fallback)
    {
        return source[propertyName] is JsonValue value && value.TryGetValue<int>(out var result)
            ? result
            : fallback;
    }
    [HttpPost("start-analyze-quick-job")]
    public async Task<IActionResult> StartAnalyzeQuickJob([FromBody] ImportRequest request)
    {
        if (string.IsNullOrEmpty(request?.Url))
            return BadRequest(new { status = "error", message = "URL missing" });
        try
        {
            var resultJson = await _pythonClient.StartAnalyzeQuickJobAsync(request.Url);
            return Content(resultJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start async quick analysis for {Url}", request.Url);
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }

    [HttpPost("start-lyrics-job")]
    public async Task<IActionResult> StartLyricsJob([FromBody] LyricsRequest request)
    {
        if (string.IsNullOrEmpty(request?.FilePath))
            return BadRequest(new { status = "error", message = "FilePath missing" });
        try
        {
            var resultJson = await _pythonClient.StartLyricsJobAsync(request.FilePath);
            return Content(resultJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start async lyrics job for {FilePath}", request.FilePath);
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }

    [HttpPost("separate-stems")]
    public async Task<IActionResult> SeparateStems([FromForm] IFormFile? file, [FromForm] string stems, [FromForm] string? originalFilePath)
    {
        if (string.IsNullOrEmpty(stems))
            return BadRequest(new { status = "error", message = "Stems list missing." });

        if (file == null && string.IsNullOrEmpty(originalFilePath))
            return BadRequest(new { status = "error", message = "No file uploaded or file path provided." });

        try
        {
            _logger.LogInformation("SeparateStems: delegating to Python Engine for stem separation.");
            var resultJson = await _pythonClient.SeparateStemsAsync(file, stems, originalFilePath);
            return Content(resultJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform stem separation.");
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }

    [HttpPost("analyze-quick")]
    public async Task<IActionResult> AnalyzeQuick(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { status = "error", message = "No file uploaded." });

        try
        {
            _logger.LogInformation("AnalyzeQuick: delegating to Python Engine for quick analysis.");
            var resultJson = await _pythonClient.AnalyzeQuickAsync(file);
            return Content(resultJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform quick analysis.");
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }
}

public class ImportRequest
{
    public string Url { get; set; } = string.Empty;
    public string? FilePath { get; set; }
}

public class LyricsRequest
{
    public string FilePath { get; set; } = string.Empty;
}
