using AiMusicWorkstation.Infrastructure.ExternalServices;
using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
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

            // If job is done, save to DB
            try
            {
                using var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("status", out var s) && s.GetString() == "done" &&
                    root.TryGetProperty("result", out var resultProp) && resultProp.ValueKind == JsonValueKind.Object)
                {
                    string title      = resultProp.TryGetProperty("title",  out var tp) && tp.ValueKind != JsonValueKind.Null ? tp.GetString() ?? "Unknown Track" : "Unknown Track";
                    string artist     = resultProp.TryGetProperty("artist", out var ap) && ap.ValueKind != JsonValueKind.Null ? ap.GetString() ?? "Unknown Artist" : "Unknown Artist";
                    double bpm        = resultProp.TryGetProperty("bpm",    out var bp)  ? bp.GetDouble()  : 120.0;
                    string key        = resultProp.TryGetProperty("key",    out var kp)  && kp.ValueKind != JsonValueKind.Null ? kp.GetString() ?? "C" : "C";
                    string stemsPath  = resultProp.TryGetProperty("stems_path", out var sp) && sp.ValueKind != JsonValueKind.Null ? sp.GetString() ?? "" : "";
                    int    timeSig    = resultProp.TryGetProperty("time_signature", out var ts) ? ts.GetInt32() : 4;
                    double durationSec= resultProp.TryGetProperty("duration_seconds", out var ds) ? ds.GetDouble() : 180.0;

                    // Deduplicate: skip save if a record with the same stems path already exists for this user
                    bool isDuplicate = false;
                    if (!string.IsNullOrEmpty(stemsPath))
                    {
                        var userProjects = await _repository.GetAllAsync(userId);
                        isDuplicate = userProjects.Any(p =>
                            !string.IsNullOrEmpty(p.StemsPath) &&
                            string.Equals(p.StemsPath, stemsPath, StringComparison.OrdinalIgnoreCase));
                    }

                    if (!isDuplicate)
                    {
                        var project = new SongProject
                        {
                            Id            = Guid.NewGuid().ToString(),
                            Title         = title,
                            Artist        = artist,
                            Bpm           = bpm,
                            Key           = key,
                            StemsPath     = stemsPath,
                            OriginalPath  = "",
                            Duration      = TimeSpan.FromSeconds(durationSec),
                            TimeSignature = timeSig,
                            Genre         = "Uncategorized",
                            DateAdded     = DateTime.Now,
                            BpmSource     = DataSource.Analysis,
                            KeySource     = DataSource.Analysis,
                            TimeSigSource = DataSource.Analysis,
                            UserId        = userId
                        };
                        await _repository.AddAsync(project);
                        await _repository.SaveAsync();
                        _logger.LogInformation("Saved async imported project: {Title} for user {UserId}", project.Title, userId);
                    }
                    else
                    {
                        _logger.LogInformation("Skipped duplicate save for project: {Title} (stems path already exists for user {UserId})", title, userId);
                    }
                }
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Failed to save job result to DB for jobId {JobId}", jobId);
            }

            return Content(resultJson, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
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
