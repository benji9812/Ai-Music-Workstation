namespace AiMusicWorkstation.Shared.Models;

public record InviteTokenValidationRequest(string Token);
public record InviteTokenValidationResponse(bool IsValid, string? SessionTicket = null);
public record InviteSessionValidationRequest(string SessionTicket);
public record InviteSessionValidationResponse(bool IsValid);
