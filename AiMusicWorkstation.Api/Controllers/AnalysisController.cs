using AiMusicWorkstation.Infrastructure.ExternalServices;
using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace AiMusicWorkstation.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
    private readonly PythonEngineClient _client;
    private readonly ILibraryRepository _repository;

    public AnalysisController(PythonEngineClient client, ILibraryRepository repository)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
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

    // GET /api/analysis/health
    [AllowAnonymous]
    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        bool healthy = await _client.IsHealthyAsync();
        return Ok(new { status = healthy ? "ok" : "error" });
    }

    // POST /api/analysis/analyze
    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromForm] IFormFile file)
    {
        if (file == null)
            return BadRequest(new { status = "error", message = "File missing" });

        var userId = GetUserId();
        string response = await _client.AnalyzeAsync(file);

        try
        {
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;
            if (root.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "success")
            {
                string title = Path.GetFileNameWithoutExtension(file.FileName);
                string artist = "Local Upload";
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
                    OriginalPath = stemsPath,
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
            }
        }
        catch (Exception)
        {
            // Do not fail request if db saving fails
        }

        return Content(response, "application/json");
    }

    // POST /api/analysis/analyze-only
    [HttpPost("analyze-only")]
    public async Task<IActionResult> AnalyzeOnly([FromForm] IFormFile file)
    {
        if (file == null)
            return BadRequest(new { status = "error", message = "File missing" });

        string response = await _client.ReAnalyzeAsync(file);
        return Content(response, "application/json");
    }

    // POST /api/analysis/structure
    [HttpPost("structure")]
    public async Task<IActionResult> Structure([FromBody] StructureRequest request)
    {
        if (request == null)
            return BadRequest(new { status = "error", message = "Payload missing" });

        // Try to find the file path if it's missing but we have a projectId
        var filePath = request.filePath;
        if (string.IsNullOrEmpty(filePath) && !string.IsNullOrEmpty(request.projectId))
        {
            var userId = GetUserId();
            var project = await _repository.GetByIdAsync(request.projectId, userId);
            if (project != null && !string.IsNullOrEmpty(project.OriginalPath))
            {
                filePath = project.OriginalPath;
            }
        }

        // Create the payload for Python Engine
        var pythonRequest = new
        {
            artist = request.artist,
            title = request.title,
            duration = request.duration,
            file_path = filePath
        };

        string response = await _client.GetStructureAsync(pythonRequest);
        return Content(response, "application/json");
    }

    // POST /api/analysis/start-structure-job
    [HttpPost("start-structure-job")]
    public async Task<IActionResult> StartStructureJob([FromBody] StructureRequest request)
    {
        if (request == null)
            return BadRequest(new { status = "error", message = "Payload missing" });

        var filePath = request.filePath;
        if (string.IsNullOrEmpty(filePath) && !string.IsNullOrEmpty(request.projectId))
        {
            var userId = GetUserId();
            var project = await _repository.GetByIdAsync(request.projectId, userId);
            if (project != null && !string.IsNullOrEmpty(project.OriginalPath))
            {
                filePath = project.OriginalPath;
            }
        }

        var pythonRequest = new
        {
            artist = request.artist,
            title = request.title,
            duration = request.duration,
            file_path = filePath
        };

        string response = await _client.StartStructureJobAsync(pythonRequest);
        return Content(response, "application/json");
    }

    // GET /api/analysis/audio/{path}
    [AllowAnonymous]
    [HttpGet("audio/{*path}")]
    public async Task<IActionResult> GetAudio(string path, [FromServices] PythonEngineConfig config, [FromServices] IHttpClientFactory clientFactory)
    {
        var client = clientFactory.CreateClient();
        var response = await client.GetAsync($"{config.BaseUrl.TrimEnd('/')}/audio/{path}", HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "Audio not found on Python engine.");

        var stream = await response.Content.ReadAsStreamAsync();
        return File(stream, "audio/mpeg", enableRangeProcessing: true);
    }

    [HttpGet("project/{id}")]
    public async Task<IActionResult> GetProjectAnalysis(string id)
    {
        var userId = GetUserId();
        var project = await _repository.GetByIdAsync(id, userId);
        if (project == null || string.IsNullOrEmpty(project.StemsPath))
        {
            return NotFound(new { status = "error", message = "Project or stems path not found." });
        }

        var analysisJson = await _client.GetAnalysisAsync(project.StemsPath);
        return Content(analysisJson, "application/json");
    }

    [HttpPatch("/api/songs/{id}/structure")]
    public async Task<IActionResult> UpdateStructure(string id, [FromBody] UpdateStructureRequest request)
    {
        var userId = GetUserId();
        var project = await _repository.GetByIdAsync(id, userId);
        if (project == null)
        {
            return NotFound(new { status = "error", message = "Project not found." });
        }

        project.Sections = JsonSerializer.Serialize(request.sections);
        project.SectionsSource = DataSource.Analysis; // Or a new source if we want to distinguish user edits

        await _repository.UpdateAsync(project);
        await _repository.SaveAsync();

        return Ok(new { status = "success" });
    }
}

public record StructureRequest(string artist, string title, double duration, string? projectId = null, string? filePath = null);
public record UpdateStructureRequest(List<SectionDto> sections);
public record SectionDto(string label, double start, double end);
