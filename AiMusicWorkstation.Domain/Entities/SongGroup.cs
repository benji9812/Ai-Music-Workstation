using System;
using System.Collections.Generic;

namespace AiMusicWorkstation.Domain.Entities;

public class SongGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }

    // Navigation property
    public ICollection<SongProject> Songs { get; set; } = new List<SongProject>();
}
