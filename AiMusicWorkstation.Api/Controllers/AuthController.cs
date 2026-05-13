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
        var token = await _dbContext.InviteTokens
            .FirstOrDefaultAsync(candidate => candidate.Token == normalizedToken, cancellationToken);

        if (token == null || !token.IsActive || token.IsUsed)
        {
            return Unauthorized(new InviteTokenValidationResponse(false));
        }

        token.IsUsed = true;
        token.UsedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new InviteTokenValidationResponse(true));
    }
}
