using System.Diagnostics;

namespace AiMusicWorkstation.Infrastructure.ExternalServices;

public class PythonBridge
{
    private readonly HttpClient _client;
    private readonly string _localPythonPath;
    private readonly string _serverPath;
    private readonly PythonConfig _config;
    private readonly SemaphoreSlim _serverStartLock = new SemaphoreSlim(1, 1);

    public PythonBridge(PythonConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _client = new HttpClient
        {
            BaseAddress = new Uri(_config.BaseUrl),
            Timeout = _config.Timeout
        };

        _localPythonPath = _config.PythonExecutablePath;
        _serverPath = _config.ServerScriptPath;

        // Starta servern asynkront så att UI:t inte låser sig vid uppstart
        Task.Run(() => EnsureServerIsRunningAsync());
    }

    private async Task EnsureServerIsRunningAsync()
    {
        await _serverStartLock.WaitAsync();
        try
        {
            if (await IsServerHealthyAsync())
                return;

            StartPythonServer();
            await Task.Delay(3000); // Ge Python tid att starta innan första anrop
        }
        finally
        {
            _serverStartLock.Release();
        }
    }

    private async Task<bool> IsServerHealthyAsync()
    {
        try
        {
            var response = await _client.GetAsync("health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private void StartPythonServer()
    {
        if (!File.Exists(_localPythonPath) || !File.Exists(_serverPath)) return;

        try
        {
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = _localPythonPath,
                Arguments = $"\"{_serverPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true, // Döljer terminalfönstret
                WorkingDirectory = Path.GetDirectoryName(_serverPath)
            };

            Process.Start(start);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Kunde inte starta Python-servern: " + ex.Message);
        }
    }

    public async Task<string> RunAnalysisAsync(string audioFilePath, bool useCloud = true)
    {
        // Vi tvingar användning av servern för att få stems
        if (useCloud)
        {
            return await AnalyzeViaApiAsync(audioFilePath);
        }
        return "{\"status\":\"error\", \"message\":\"Cloud mode required for stems\"}";
    }

    private async Task<string> AnalyzeViaApiAsync(string filePath)
    {
        if (!File.Exists(filePath)) return "{\"error\": \"Filen hittades inte.\"}";

        for (int i = 0; i < 3; i++)
        {
            try
            {
                await EnsureServerIsRunningAsync();
                using var content = new MultipartFormDataContent();
                byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
                content.Add(new ByteArrayContent(fileBytes), "file", Path.GetFileName(filePath));

                var response = await _client.PostAsync("analyze", content);
                string body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode) return body;

                Debug.WriteLine($"[AnalyzeViaApi] Attempt {i + 1} HTTP {(int)response.StatusCode}: {body[..Math.Min(200, body.Length)]}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AnalyzeViaApi] Attempt {i + 1} Exception: {ex.Message}");
                await Task.Delay(2000);
            }
        }
        return "{\"error\": \"Kunde inte nå AI-motorn. Kontrollera Python-miljön.\"}";
    }

    public async Task<string> ReAnalyzeAsync(string filePath)
    {
        if (!File.Exists(filePath)) return "{\"status\":\"error\", \"message\":\"Fil saknas\"}";

        try
        {
            await EnsureServerIsRunningAsync();
            using var content = new MultipartFormDataContent();
            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
            content.Add(new ByteArrayContent(fileBytes), "file", Path.GetFileName(filePath));

            var response = await _client.PostAsync("analyze-only", content);
            string body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                Debug.WriteLine($"[ReAnalyze] HTTP {(int)response.StatusCode}: {body[..Math.Min(200, body.Length)]}");

            return body;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ReAnalyze] Exception: {ex.Message}");
            return "{\"status\":\"error\"}";
        }
    }

    public async Task<string> GetStructureAsync(string artist, string title, double duration)
    {
        try
        {
            await EnsureServerIsRunningAsync();
            var payload = new { artist, title, duration };
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("structure", content);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();
        }
        catch { }
        return "{\"status\":\"error\"}";
    }
}