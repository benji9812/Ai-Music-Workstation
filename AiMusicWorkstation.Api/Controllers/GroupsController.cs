using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AiMusicWorkstation.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class GroupsController : ControllerBase
{
    private readonly AiMusicWorkstationDbContext _context;

    public GroupsController(AiMusicWorkstationDbContext context)
    {
        _context = context;
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

    [HttpGet]
    public async Task<IActionResult> GetGroups()
    {
        var userId = GetUserId();
        var groups = await _context.SongGroups
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.Name)
            .ToListAsync();
        return Ok(groups);
    }

    [HttpPost]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Name is required");

        var userId = GetUserId();
        var group = new SongGroup
        {
            Name = dto.Name,
            UserId = userId
        };
        _context.SongGroups.Add(group);
        await _context.SaveChangesAsync();

        return Ok(group);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateGroup(Guid id, [FromBody] UpdateGroupDto dto)
    {
        var userId = GetUserId();
        var group = await _context.SongGroups.FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);
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
        var userId = GetUserId();
        var group = await _context.SongGroups.FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);
        if (group == null) return NotFound();

        _context.SongGroups.Remove(group);
        await _context.SaveChangesAsync();

        return Ok(new { status = "success" });
    }
}

public record CreateGroupDto(string Name);
public record UpdateGroupDto(string Name);
