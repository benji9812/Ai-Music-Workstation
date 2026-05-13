namespace AiMusicWorkstation.Domain.Entities;

public class InviteToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Token { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsUsed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}
