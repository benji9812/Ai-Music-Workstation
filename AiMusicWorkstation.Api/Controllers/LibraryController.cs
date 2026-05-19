using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AiMusicWorkstation.Infrastructure.Persistence;
using AiMusicWorkstation.Infrastructure.ExternalServices;
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
    public async Task<IActionResult> DeleteProject(string id)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest();
        
        await _repository.DeleteAsync(id);
        await _repository.SaveAsync();
        
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
}
