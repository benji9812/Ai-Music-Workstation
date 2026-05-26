using System.Text.Json.Serialization;

namespace AiMusicWorkstation.Shared.Dto;

public class SeparateStemsRequest
{
    [JsonPropertyName("stems")]
    public List<string> Stems { get; set; } = new();

    [JsonPropertyName("file_path")]
    public string? FilePath { get; set; }
}
