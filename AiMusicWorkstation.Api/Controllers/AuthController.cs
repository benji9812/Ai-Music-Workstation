using AiMusicWorkstation.Infrastructure.Persistence;
using AiMusicWorkstation.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiMusicWorkstation.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AiMusicWorkstationDbContext _dbContext;

    public AuthController(AiMusicWorkstationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost("validate-invite")]
    public async Task<ActionResult<InviteTokenValidationResponse>> ValidateInvite(
        [FromBody] InviteTokenValidationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new InviteTokenValidationResponse(false));
        }

        string normalizedToken = request.Token.Trim();
        int updated = await _dbContext.InviteTokens
            .Where(candidate => candidate.Token == normalizedToken && candidate.IsActive && !candidate.IsUsed)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(candidate => candidate.IsUsed, true)
                    .SetProperty(candidate => candidate.UsedAt, DateTimeOffset.UtcNow),
                cancellationToken);

        if (updated == 0)
        {
            return Unauthorized(new InviteTokenValidationResponse(false));
        }

        return Ok(new InviteTokenValidationResponse(true));
    }
}
