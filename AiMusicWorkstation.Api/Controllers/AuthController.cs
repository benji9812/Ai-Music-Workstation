using AiMusicWorkstation.Infrastructure.Persistence;
using AiMusicWorkstation.Shared.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace AiMusicWorkstation.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AiMusicWorkstationDbContext _dbContext;
    private readonly IDataProtector _inviteSessionProtector;
    private static readonly TimeSpan InviteSessionLifetime = TimeSpan.FromDays(30);

    public AuthController(AiMusicWorkstationDbContext dbContext, IDataProtectionProvider dataProtectionProvider)
    {
        _dbContext = dbContext;
        _inviteSessionProtector = dataProtectionProvider.CreateProtector("AiMusicWorkstation.InviteSession.v1");
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

        long expiresAtUnixSeconds = DateTimeOffset.UtcNow.Add(InviteSessionLifetime).ToUnixTimeSeconds();
        string payload = $"{expiresAtUnixSeconds}|{Guid.NewGuid():N}";
        string sessionTicket = _inviteSessionProtector.Protect(payload);

        return Ok(new InviteTokenValidationResponse(true, sessionTicket));
    }

    [HttpPost("validate-session")]
    public ActionResult<InviteSessionValidationResponse> ValidateSession([FromBody] InviteSessionValidationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SessionTicket))
        {
            return BadRequest(new InviteSessionValidationResponse(false));
        }

        try
        {
            string payload = _inviteSessionProtector.Unprotect(request.SessionTicket);
            string[] parts = payload.Split('|', 2);
            if (parts.Length != 2 || !long.TryParse(parts[0], out long expiresAtUnixSeconds))
            {
                return Unauthorized(new InviteSessionValidationResponse(false));
            }

            bool isExpired = DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAtUnixSeconds;
            if (isExpired)
            {
                return Unauthorized(new InviteSessionValidationResponse(false));
            }

            return Ok(new InviteSessionValidationResponse(true));
        }
        catch (CryptographicException)
        {
            return Unauthorized(new InviteSessionValidationResponse(false));
        }
    }
}
