using System.Diagnostics;

namespace AiMusicWorkstation.Api.Services;

public class PythonEngineManager
{
    private readonly HttpClient _client;
    private readonly PythonEngineConfig _config;
    private readonly SemaphoreSlim _serverStartLock = new SemaphoreSlim(1, 1);

    public PythonEngineManager(HttpClient client, PythonEngineConfig config)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task EnsureRunningAsync()
    {
        await _serverStartLock.WaitAsync();
        try
        {
            if (await IsHealthyAsync())
                return;

            StartPythonServer();
            await Task.Delay(3000);
        }
        finally
        {
            _serverStartLock.Release();
        }
    }

    public async Task<bool> IsHealthyAsync()
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
        if (!File.Exists(_config.PythonExecutablePath) || !File.Exists(_config.ServerScriptPath))
            return;

        try
        {
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = _config.PythonExecutablePath,
                Arguments = $"\"{_config.ServerScriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_config.ServerScriptPath)
            };

            Process.Start(start);
        }
        catch
        {
        }
    }
}
