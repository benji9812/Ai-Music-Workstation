using AiMusicWorkstation.Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Mvc;

namespace AiMusicWorkstation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
    private readonly PythonEngineClient _client;

    public AnalysisController(PythonEngineClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    // GET /api/analysis/health
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

        string response = await _client.AnalyzeAsync(file);
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

        string response = await _client.GetStructureAsync(request);
        return Content(response, "application/json");
    }

    // GET /api/analysis/audio/{path}
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
}

public record StructureRequest(string artist, string title, double duration);