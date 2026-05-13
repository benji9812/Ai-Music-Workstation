using AiMusicWorkstation.Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Mvc;

namespace AiMusicWorkstation.Api.Controllers;

[ApiController]
[Route("")]
public class AnalysisController : ControllerBase
{
    private readonly PythonEngineClient _client;

    public AnalysisController(PythonEngineClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        bool healthy = await _client.IsHealthyAsync();
        return Ok(new { status = healthy ? "ok" : "error" });
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromForm] IFormFile file)
    {
        if (file == null) return BadRequest(new { status = "error", message = "File missing" });
        string response = await _client.AnalyzeAsync(file);
        return Content(response, "application/json");
    }

    [HttpPost("analyze-only")]
    public async Task<IActionResult> AnalyzeOnly([FromForm] IFormFile file)
    {
        if (file == null) return BadRequest(new { status = "error", message = "File missing" });
        string response = await _client.ReAnalyzeAsync(file);
        return Content(response, "application/json");
    }

    [HttpPost("structure")]
    public async Task<IActionResult> Structure([FromBody] StructureRequest request)
    {
        if (request == null) return BadRequest(new { status = "error", message = "Payload missing" });
        string response = await _client.GetStructureAsync(request);
        return Content(response, "application/json");
    }
}

public record StructureRequest(string artist, string title, double duration);
