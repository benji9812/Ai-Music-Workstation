using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AiMusicWorkstation.Infrastructure.Persistence;
using AiMusicWorkstation.Infrastructure.ExternalServices;
using System.Text.Json;
using Npgsql;

namespace AiMusicWorkstation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LibraryController : ControllerBase
{
    private readonly ILibraryRepository _repository;

    public LibraryController(ILibraryRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    [HttpGet("db-check")]
    public async Task<IActionResult> DbCheck(
        [FromServices] AiMusicWorkstationDbContext context,
        [FromServices] PythonEngineConfig pythonConfig)
    {
        string connectionString = "";
        string sanitizedConnString = "";
        try
        {
            connectionString = context.Database.GetConnectionString() ?? "";
            sanitizedConnString = connectionString;
            try
            {
                var cb = new NpgsqlConnectionStringBuilder(connectionString);
                if (!string.IsNullOrEmpty(cb.Password)) cb.Password = "***";
                sanitizedConnString = cb.ConnectionString;
            }
            catch (Exception pex)
            {
                sanitizedConnString = $"[Parse Error: {pex.Message}] Raw: {connectionString}";
            }
        }
        catch (Exception ex)
        {
            sanitizedConnString = $"[GetConnectionString Error: {ex.Message}]";
        }

        try
        {
            var canConnect = await context.Database.CanConnectAsync();
            var migrations = await context.Database.GetAppliedMigrationsAsync();
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            
            int projectCount = 0;
            string tableStatus = "Unknown";
            try
            {
                projectCount = await context.Projects.CountAsync();
                tableStatus = "Exists and is accessible";
            }
            catch (Exception ex)
            {
                tableStatus = $"Error: {ex.Message}";
            }

            return Ok(new {
                canConnect,
                tableStatus,
                projectCount,
                appliedMigrations = migrations,
                pendingMigrations = pendingMigrations,
                connectionStringConfigured = !string.IsNullOrEmpty(connectionString),
                sanitizedConnectionString = sanitizedConnString,
                pythonEngineBaseUrl = pythonConfig.BaseUrl
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new {
                error = ex.Message,
                stackTrace = ex.StackTrace,
                innerError = ex.InnerException?.Message,
                sanitizedConnectionString = sanitizedConnString
            });
        }
    }

    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects()
    {
        var projects = await _repository.GetAllAsync();
        return Ok(projects);
    }

    [HttpDelete("projects/{id}")]
    public async Task<IActionResult> DeleteProject(string id, [FromQuery] bool deleteFiles = true)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest();

        var project = await _repository.GetByIdAsync(id);
        if (project == null) return NotFound();

        await _repository.DeleteAsync(id);
        await _repository.SaveAsync();

        if (deleteFiles)
        {
            var fileManager = HttpContext.RequestServices.GetRequiredService<AiMusicWorkstation.Domain.Services.IProjectFileManager>();
            await fileManager.DeleteProjectFilesAsync(project);
        }
        
        return Ok(new { status = "success" });
    }

    [HttpPut("projects/{id}")]
    public async Task<IActionResult> UpdateProject(string id, [FromBody] SongProject project)
    {
        if (id != project.Id) return BadRequest();
        
        await _repository.UpdateAsync(project);
        await _repository.SaveAsync();
        
        return Ok(new { status = "success" });
    }

    [HttpPost("rescan-stems")]
    public async Task<IActionResult> RescanStems(
        [FromServices] PythonEngineClient pythonClient,
        [FromServices] ILogger<LibraryController> logger)
    {
        try
        {
            var json = await pythonClient.RescanStemsAsync();
            using var doc = JsonDocument.Parse(json);
            var stemsArr = doc.RootElement.GetProperty("stems");

            // Get all existing stems paths from DB
            var existing = await _repository.GetAllAsync();
            var existingPaths = new HashSet<string>(
                existing.Where(p => !string.IsNullOrEmpty(p.StemsPath))
                        .Select(p => p.StemsPath),
                StringComparer.OrdinalIgnoreCase);

            int added = 0;
            foreach (var stem in stemsArr.EnumerateArray())
            {
                var stemsPath = stem.GetProperty("stems_path").GetString() ?? "";
                if (string.IsNullOrEmpty(stemsPath) || existingPaths.Contains(stemsPath))
                    continue;

                var folderName = stem.GetProperty("folder_name").GetString() ?? "Unknown";
                var title = stem.TryGetProperty("title", out var tp) ? tp.GetString() : folderName;

                var project = new SongProject
                {
                    Id            = Guid.NewGuid().ToString(),
                    Title         = title ?? folderName,
                    Artist        = "Unknown Artist",
                    Bpm           = 120,
                    Key           = "C",
                    StemsPath     = stemsPath,
                    OriginalPath  = "",
                    Duration      = TimeSpan.FromMinutes(3),
                    TimeSignature = 4,
                    Genre         = "Uncategorized",
                    DateAdded     = DateTime.Now,
                    BpmSource     = DataSource.Analysis,
                    KeySource     = DataSource.Analysis,
                    TimeSigSource = DataSource.Analysis
                };

                await _repository.AddAsync(project);
                existingPaths.Add(stemsPath);
                added++;
                logger.LogInformation("Rescan: added orphaned stems '{Title}' from {Path}", title, stemsPath);
            }

            if (added > 0) await _repository.SaveAsync();

            return Ok(new { status = "success", added, total = existingPaths.Count });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rescan stems failed");
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }
}
