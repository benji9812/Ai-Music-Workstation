using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Api.Services;

public class PythonEngineManager
{
    private readonly HttpClient _client;
    private readonly PythonEngineConfig _config;
    private readonly ILogger<PythonEngineManager> _logger;
    private readonly SemaphoreSlim _serverStartLock = new SemaphoreSlim(1, 1);

    public PythonEngineManager(HttpClient client, PythonEngineConfig config, ILogger<PythonEngineManager> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnsureRunningAsync()
    {
        await _serverStartLock.WaitAsync();
        try
        {
            if (await IsHealthyAsync())
                return;

            StartPythonServer();
            await WaitForHealthyAsync();
        }
        finally
        {
            _serverStartLock.Release();
        }
    }

    private async Task WaitForHealthyAsync()
    {
        const int maxAttempts = 10;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await Task.Delay(1000);
            if (await IsHealthyAsync())
            {
                _logger.LogInformation("Python engine healthy after {Attempts} attempts.", attempt);
                return;
            }
        }

        _logger.LogWarning("Python engine did not become healthy after {Attempts} attempts.", maxAttempts);
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
        string pythonPath = ResolvePythonExecutable();
        if (string.IsNullOrWhiteSpace(pythonPath) || !File.Exists(_config.ServerScriptPath))
        {
            _logger.LogError(
                "Python engine not started. Python executable or script missing. PythonPath={PythonPath}, ScriptPath={ScriptPath}",
                pythonPath, _config.ServerScriptPath);
            return;
        }

        try
        {
            _logger.LogInformation(
                "Starting Python engine. PythonPath={PythonPath}, ScriptPath={ScriptPath}, WorkingDirectory={WorkingDirectory}",
                pythonPath,
                _config.ServerScriptPath,
                Path.GetDirectoryName(_config.ServerScriptPath));
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"\"{_config.ServerScriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_config.ServerScriptPath)
            };

            Process.Start(start);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Python engine process.");
        }
    }

    private string ResolvePythonExecutable()
    {
        if (File.Exists(_config.PythonExecutablePath))
            return _config.PythonExecutablePath;

        _logger.LogWarning("Configured Python executable not found: {PythonPath}", _config.PythonExecutablePath);
        return "python";
    }
}
