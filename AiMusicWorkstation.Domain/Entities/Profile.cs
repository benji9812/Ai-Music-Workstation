using System;

namespace AiMusicWorkstation.Domain.Entities;

public class Profile
{
    public Guid Id { get; set; }
    public string Role { get; set; } = "user";
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
