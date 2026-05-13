namespace AiMusicWorkstation.Shared.Models;

public record InviteTokenValidationRequest(string Token);
public record InviteTokenValidationResponse(bool IsValid);
