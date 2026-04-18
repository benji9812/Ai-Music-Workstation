using Microsoft.Extensions.Configuration;
using System.IO;

namespace AiMusicWorkstation.Desktop.Services;

public class PythonConfig
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:8000/";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);
    public string SolutionRoot { get; set; } = string.Empty;
    public string PythonExecutablePath { get; set; } = string.Empty;
    public string ServerScriptPath { get; set; } = string.Empty;

    public static PythonConfig CreateDefault()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string solutionRoot = Path.GetFullPath(Path.Combine(baseDir, "../../../.."));

        return new PythonConfig
        {
            SolutionRoot = solutionRoot,
            PythonExecutablePath = Path.Combine(solutionRoot, "PythonEngine", "venv", "Scripts", "python.exe"),
            ServerScriptPath = Path.Combine(solutionRoot, "PythonEngine", "main.py")
        };
    }

    public static PythonConfig FromConfiguration(IConfiguration? configuration)
    {
        var config = CreateDefault();
        if (configuration == null) return config;

        string? baseUrl = configuration["Python:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
            config.BaseUrl = baseUrl;

        if (int.TryParse(configuration["Python:TimeoutMinutes"], out int timeoutMinutes))
            config.Timeout = TimeSpan.FromMinutes(timeoutMinutes);

        string? solutionRoot = configuration["Python:SolutionRoot"];
        if (!string.IsNullOrWhiteSpace(solutionRoot))
        {
            config.SolutionRoot = solutionRoot;
            config.PythonExecutablePath = Path.Combine(solutionRoot, "PythonEngine", "venv", "Scripts", "python.exe");
            config.ServerScriptPath = Path.Combine(solutionRoot, "PythonEngine", "main.py");
        }

        string? pythonPath = configuration["Python:ExecutablePath"];
        if (!string.IsNullOrWhiteSpace(pythonPath))
            config.PythonExecutablePath = pythonPath;

        string? serverPath = configuration["Python:ServerScriptPath"];
        if (!string.IsNullOrWhiteSpace(serverPath))
            config.ServerScriptPath = serverPath;

        return config;
    }
}
