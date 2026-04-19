using Microsoft.Extensions.Configuration;
using System.IO;

namespace AiMusicWorkstation.Api.Services;

public class PythonEngineConfig
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:8000/";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);
    public string SolutionRoot { get; set; } = string.Empty;
    public string PythonExecutablePath { get; set; } = string.Empty;
    public string ServerScriptPath { get; set; } = string.Empty;

    public static PythonEngineConfig CreateDefault()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string solutionRoot = Path.GetFullPath(Path.Combine(baseDir, "../../../.."));

        return new PythonEngineConfig
        {
            SolutionRoot = solutionRoot,
            PythonExecutablePath = Path.Combine(solutionRoot, "PythonEngine", "venv", "Scripts", "python.exe"),
            ServerScriptPath = Path.Combine(solutionRoot, "PythonEngine", "main.py")
        };
    }

    public static PythonEngineConfig FromConfiguration(IConfiguration configuration)
    {
        var config = CreateDefault();

        string? baseUrl = configuration["PythonEngine:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
            config.BaseUrl = baseUrl;

        if (int.TryParse(configuration["PythonEngine:TimeoutMinutes"], out int timeoutMinutes))
            config.Timeout = TimeSpan.FromMinutes(timeoutMinutes);

        string? solutionRoot = configuration["PythonEngine:SolutionRoot"];
        if (!string.IsNullOrWhiteSpace(solutionRoot))
        {
            config.SolutionRoot = solutionRoot;
            config.PythonExecutablePath = Path.Combine(solutionRoot, "PythonEngine", "venv", "Scripts", "python.exe");
            config.ServerScriptPath = Path.Combine(solutionRoot, "PythonEngine", "main.py");
        }

        string? pythonPath = configuration["PythonEngine:ExecutablePath"];
        if (!string.IsNullOrWhiteSpace(pythonPath))
            config.PythonExecutablePath = pythonPath;

        string? serverPath = configuration["PythonEngine:ServerScriptPath"];
        if (!string.IsNullOrWhiteSpace(serverPath))
            config.ServerScriptPath = serverPath;

        return config;
    }
}
