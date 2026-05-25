using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AiMusicWorkstation.Infrastructure.Persistence;
using AiMusicWorkstation.Infrastructure.ExternalServices;
using System.Text.Json;
using Npgsql;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace AiMusicWorkstation.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class LibraryController : ControllerBase
{
    private readonly ILibraryRepository _repository;

    public LibraryController(ILibraryRepository repository)
    {
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

    [AllowAnonymous]
    [HttpGet("db-check")]
    public async Task<IActionResult> DbCheck(
        [FromServices] AiMusicWorkstationDbContext context,
        [FromServices] PythonEngineConfig pythonConfig)
    {
        // ... (rest of the method remains the same)
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
        var projects = await _repository.GetAllAsync(GetUserId());
        return Ok(projects);
    }

    [HttpDelete("projects/{id}")]
    public async Task<IActionResult> DeleteProject(string id, [FromQuery] bool deleteFiles = true)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest();

        var userId = GetUserId();
        var project = await _repository.GetByIdAsync(id, userId);
        if (project == null) return NotFound();

        await _repository.DeleteAsync(id, userId);
        await _repository.SaveAsync();

        if (deleteFiles)
        {
            var fileManager = HttpContext.RequestServices.GetRequiredService<AiMusicWorkstation.Domain.Services.IProjectFileManager>();
            await fileManager.DeleteProjectFilesAsync(project);
        }

        return Ok(new { status = "success" });
    }

    [HttpPatch("projects/{id}")]
    public async Task<IActionResult> PatchProject(string id, [FromBody] UpdateProjectDto projectDto)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest();

        var userId = GetUserId();
        var existingProject = await _repository.GetByIdAsync(id, userId);
        if (existingProject == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(projectDto.Title))
        {
            existingProject.Title = projectDto.Title;
        }
        if (!string.IsNullOrWhiteSpace(projectDto.Artist))
        {
            existingProject.Artist = projectDto.Artist;
        }

        await _repository.UpdateAsync(existingProject);
        await _repository.SaveAsync();

        return Ok(new { status = "success" });
    }

    [HttpPatch("projects/{id}/group")]
    public async Task<IActionResult> PatchProjectGroup(string id, [FromBody] UpdateProjectGroupDto dto)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest();

        var userId = GetUserId();
        var existingProject = await _repository.GetByIdAsync(id, userId);
        if (existingProject == null) return NotFound();

        if (dto.GroupId == null)
        {
            existingProject.GroupId = null;
        }
        else
        {
            existingProject.GroupId = dto.GroupId;
        }

        await _repository.UpdateAsync(existingProject);
        await _repository.SaveAsync();

        return Ok(new { status = "success" });
    }

    [Authorize(Roles = "admin")]
    [HttpPost("rescan-stems")]
    public async Task<IActionResult> RescanStems(
        [FromServices] PythonEngineClient pythonClient,
        [FromServices] ILogger<LibraryController> logger)
    {
        int added = 0;
        var userId = GetUserId();
        try
        {
            var json = await pythonClient.RescanStemsAsync();
            using var doc = JsonDocument.Parse(json);
            var stemsArr = doc.RootElement.GetProperty("stems");

            // Get all existing stems paths from DB for this user
            var existing = await _repository.GetAllAsync(userId);
            var existingPaths = new HashSet<string>(
                existing.Where(p => !string.IsNullOrEmpty(p.StemsPath))
                        .Select(p => p.StemsPath),
                StringComparer.OrdinalIgnoreCase);

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
                    DateAdded     = DateTime.UtcNow,
                    BpmSource     = DataSource.Analysis,
                    KeySource     = DataSource.Analysis,
                    TimeSigSource = DataSource.Analysis,
                    UserId        = userId
                };

                await _repository.AddAsync(project);
                existingPaths.Add(stemsPath);
                added++;
                logger.LogInformation("Rescan: added orphaned stems '{Title}' from {Path} for user {UserId}", title, stemsPath, userId);
            }

            if (added > 0)
            {
                try
                {
                    await _repository.SaveAsync();
                }
                catch (DbUpdateException ex)
                {
                    var baseMessage = ex.GetBaseException().Message;
                    logger.LogError(ex, "Rescan stems save failed after adding {Added} project(s). Root cause: {RootCause}", added, baseMessage);
                    return StatusCode(500, new { status = "error", message = BuildRescanErrorMessage(ex) });
                }
            }

            return Ok(new { status = "success", added, total = existingPaths.Count });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rescan stems failed after adding {Added} project(s). Root cause: {RootCause}", added, ex.GetBaseException().Message);
            return StatusCode(500, new { status = "error", message = BuildRescanErrorMessage(ex) });
        }
    }

    private static string BuildRescanErrorMessage(Exception exception)
    {
        var baseException = exception.GetBaseException();

        if (baseException is PostgresException postgresException)
        {
            return postgresException.SqlState switch
            {
                PostgresErrorCodes.UniqueViolation => "Rescan failed: an orphaned stem with the same path already exists.",
                PostgresErrorCodes.NotNullViolation => "Rescan failed: the database rejected a required field.",
                PostgresErrorCodes.ForeignKeyViolation => "Rescan failed: a related record is missing.",
                _ => $"Rescan failed: {postgresException.MessageText}"
            };
        }

        return $"Rescan failed: {baseException.Message}";
    }
}

public record UpdateProjectDto(string? Title, string? Artist);
public record UpdateProjectGroupDto(Guid? GroupId);
