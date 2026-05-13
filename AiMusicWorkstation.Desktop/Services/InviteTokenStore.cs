using System.IO;
using System.Security.Cryptography;
using System.Text;
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

    public string? GetSessionTicket()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            byte[] protectedBytes = File.ReadAllBytes(_filePath);
            byte[] dataBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            var state = JsonSerializer.Deserialize<InviteTokenState>(Encoding.UTF8.GetString(dataBytes));
            if (state?.IsValidated != true || string.IsNullOrWhiteSpace(state.SessionTicket))
            {
                return null;
            }

            return state.SessionTicket;
        }
        catch (CryptographicException)
        {
            TryDeleteStateFile();
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void MarkValidated(string sessionTicket)
    {
        string? dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var state = new InviteTokenState
        {
            IsValidated = true,
            SessionTicket = sessionTicket,
            ValidatedAt = DateTimeOffset.UtcNow
        };

        string json = JsonSerializer.Serialize(state);
        byte[] dataBytes = Encoding.UTF8.GetBytes(json);
        byte[] protectedBytes = ProtectedData.Protect(dataBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_filePath, protectedBytes);
    }

    private sealed class InviteTokenState
    {
        public bool IsValidated { get; set; }
        public string? SessionTicket { get; set; }
        public DateTimeOffset ValidatedAt { get; set; }
    }

    public void Clear()
    {
        TryDeleteStateFile();
    }

    private void TryDeleteStateFile()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch (IOException)
        {
            // Ignored intentionally. Validation will simply require a new token.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignored intentionally. Validation will simply require a new token.
        }
    }
}
