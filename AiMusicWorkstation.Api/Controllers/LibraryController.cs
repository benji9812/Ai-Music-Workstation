using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AiMusicWorkstation.Infrastructure.Persistence;

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
    public async Task<IActionResult> DbCheck([FromServices] AiMusicWorkstationDbContext context)
    {
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
                connectionStringConfigured = !string.IsNullOrEmpty(context.Database.GetConnectionString())
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new {
                error = ex.Message,
                stackTrace = ex.StackTrace,
                innerError = ex.InnerException?.Message
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
