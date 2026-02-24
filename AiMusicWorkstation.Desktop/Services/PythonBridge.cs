using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace AiMusicWorkstation.Desktop.Services
{
    public class PythonBridge
    {
        private readonly HttpClient _client;
        private readonly string _localPythonPath;
        private readonly string _serverPath;

        public PythonBridge()
        {
            _client = new HttpClient();
            _client.BaseAddress = new Uri("http://127.0.0.1:8000/");

            // Demucs är tungt, så vi tillåter att anropet tar upp till 10 minuter
            _client.Timeout = TimeSpan.FromMinutes(10);

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string solutionRoot = Path.GetFullPath(Path.Combine(baseDir, "../../../.."));

            _localPythonPath = Path.Combine(solutionRoot, "PythonEngine", "venv", "Scripts", "python.exe");
            _serverPath = Path.Combine(solutionRoot, "PythonEngine", "main.py");

            // Starta servern asynkront så att UI:t inte låser sig vid uppstart
            Task.Run(() => EnsureServerIsRunning());
        }

        private async Task EnsureServerIsRunning()
        {
            try
            {
                // Gör en snabb kontroll om servern redan svarar
                await _client.GetAsync("");
            }
            catch
            {
                // Om servern inte svarar, starta den
                StartPythonServer();
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

            // Försök skicka filen, med inbyggd retry om servern precis håller på att starta
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    using (var content = new MultipartFormDataContent())
                    {
                        byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
                        var fileContent = new ByteArrayContent(fileBytes);
                        content.Add(fileContent, "file", Path.GetFileName(filePath));

                        var response = await _client.PostAsync("analyze", content);
                        if (response.IsSuccessStatusCode)
                        {
                            return await response.Content.ReadAsStringAsync();
                        }
                    }
                }
                catch
                {
                    // Vänta 2 sekunder innan nästa försök om servern inte är redo än
                    await Task.Delay(2000);
                }
            }
            return "{\"error\": \"Kunde inte nå AI-motorn. Kontrollera Python-miljön.\"}";
        }

        public async Task<string> ReAnalyzeAsync(string filePath)
        {
            if (!File.Exists(filePath)) return "{\"status\":\"error\"}";

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    using var content = new MultipartFormDataContent();
                    byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
                    content.Add(new ByteArrayContent(fileBytes), "file", Path.GetFileName(filePath));

                    var response = await _client.PostAsync("analyze-only", content);
                    if (response.IsSuccessStatusCode)
                        return await response.Content.ReadAsStringAsync();
                }
                catch { await Task.Delay(2000); }
            }
            return "{\"status\":\"error\"}";
        }

        public async Task<string> GetStructureAsync(string artist, string title, double duration)
        {
            try
            {
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
}