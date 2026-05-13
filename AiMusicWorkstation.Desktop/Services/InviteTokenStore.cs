using System.IO;
using System.Text.Json;

namespace AiMusicWorkstation.Desktop.Services;

public class InviteTokenStore
{
    private const string FileName = "invite.json";
    private readonly string _filePath;

    public InviteTokenStore()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _filePath = Path.Combine(appData, "AiMusicWorkstation", FileName);
    }

    public bool IsValidated()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return false;
            }

            var state = JsonSerializer.Deserialize<InviteTokenState>(File.ReadAllText(_filePath));
            return state?.IsValidated == true;
        }
        catch
        {
            return false;
        }
    }

    public void MarkValidated(string token)
    {
        string? dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var state = new InviteTokenState
        {
            IsValidated = true,
            Token = token,
            ValidatedAt = DateTimeOffset.UtcNow
        };

        string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }

    private sealed class InviteTokenState
    {
        public bool IsValidated { get; set; }
        public string? Token { get; set; }
        public DateTimeOffset ValidatedAt { get; set; }
    }
}
