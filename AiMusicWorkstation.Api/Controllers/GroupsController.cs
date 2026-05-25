using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiMusicWorkstation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GroupsController : ControllerBase
{
    private readonly AiMusicWorkstationDbContext _context;

    public GroupsController(AiMusicWorkstationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetGroups()
    {
        var groups = await _context.SongGroups
            .OrderBy(g => g.Name)
            .ToListAsync();
        return Ok(groups);
    }

    [HttpPost]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Name is required");

        var group = new SongGroup { Name = dto.Name };
        _context.SongGroups.Add(group);
        await _context.SaveChangesAsync();

        return Ok(group);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateGroup(Guid id, [FromBody] UpdateGroupDto dto)
    {
        var group = await _context.SongGroups.FindAsync(id);
        if (group == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            group.Name = dto.Name;
        }

        await _context.SaveChangesAsync();
        return Ok(group);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteGroup(Guid id)
    {
        var group = await _context.SongGroups.FindAsync(id);
        if (group == null) return NotFound();

        _context.SongGroups.Remove(group);
        await _context.SaveChangesAsync();

        return Ok(new { status = "success" });
    }
}

public record CreateGroupDto(string Name);
public record UpdateGroupDto(string Name);
