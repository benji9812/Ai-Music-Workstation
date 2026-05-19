using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

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
