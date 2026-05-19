using AiMusicWorkstation.Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Mvc;

namespace AiMusicWorkstation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    private readonly PythonEngineClient _pythonClient;
    private readonly ILogger<ImportController> _logger;

    public ImportController(
        PythonEngineClient pythonClient,
        ILogger<ImportController> logger)
    {
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
            _logger.LogInformation("ImportFromUrl: delegating to Python Engine for {Url}", request.Url);
            
            // Delegate everything to Python Engine (which has yt-dlp installed)
            var resultJson = await _pythonClient.ImportUrlAsync(request.Url);
            
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
