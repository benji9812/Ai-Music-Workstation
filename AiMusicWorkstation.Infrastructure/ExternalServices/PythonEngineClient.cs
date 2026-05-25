using Microsoft.AspNetCore.Http;
using System.Net.Http.Json;

namespace AiMusicWorkstation.Infrastructure.ExternalServices;

public class PythonEngineClient
{
    private readonly HttpClient _client;
    private readonly PythonEngineManager _manager;

    public PythonEngineClient(HttpClient client, PythonEngineManager manager)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    public async Task<string> AnalyzeAsync(IFormFile file)
    {
        if (!await _manager.EnsureRunningAsync())
            return "{\"status\":\"error\",\"message\":\"Python engine not reachable\"}";
        using var content = await BuildMultipartAsync(file);
        var response = await _client.PostAsync("analyze", content);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> AnalyzeQuickAsync(IFormFile file)
    {
        if (!await _manager.EnsureRunningAsync())
            return "{\"status\":\"error\",\"message\":\"Python engine not reachable\"}";
        using var content = await BuildMultipartAsync(file);
        var response = await _client.PostAsync("analyze-quick", content);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> ReAnalyzeAsync(IFormFile file)
    {
        if (!await _manager.EnsureRunningAsync())
            return "{\"status\":\"error\",\"message\":\"Python engine not reachable\"}";
        using var content = await BuildMultipartAsync(file);
        var response = await _client.PostAsync("analyze-only", content);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetStructureAsync(object payload)
    {
        if (!await _manager.EnsureRunningAsync())
            return "{\"status\":\"error\",\"message\":\"Python engine not reachable\"}";
        var response = await _client.PostAsJsonAsync("structure", payload);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> ImportUrlAsync(string url)
    {
        if (!await _manager.EnsureRunningAsync())
            return "{\"status\":\"error\",\"message\":\"Python engine not reachable\"}";
        var response = await _client.PostAsJsonAsync("import-url", new { url });
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> StartImportJobAsync(string url)
    {
        if (!await _manager.EnsureRunningAsync())
            return "{\"status\":\"error\",\"message\":\"Python engine not reachable\"}";
        var response = await _client.PostAsJsonAsync("import-url-async", new { url });
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> StartAnalyzeQuickJobAsync(string url)
    {
        if (!await _manager.EnsureRunningAsync())
            return "{\"status\":\"error\",\"message\":\"Python engine not reachable\"}";
        var response = await _client.PostAsJsonAsync("start-analyze-quick-job", new { url });
        return await response.Content.ReadAsStringAsync();
    }


    public async Task<string> GetJobStatusAsync(string jobId)
    {
        if (!await _manager.EnsureRunningAsync())
            return "{\"status\":\"error\",\"message\":\"Python engine not reachable\"}";
        var response = await _client.GetAsync($"job-status/{jobId}");
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> RescanStemsAsync()
    {
        if (!await _manager.EnsureRunningAsync())
            return "{\"stems\":[]}";
        var response = await _client.GetAsync("rescan-stems");
        return await response.Content.ReadAsStringAsync();
    }

    public Task<bool> IsHealthyAsync() => _manager.IsHealthyAsync();

    public async Task<string> GetAnalysisAsync(string stemsPath)
    {
        if (!await _manager.EnsureRunningAsync())
            return "{\"status\":\"error\",\"message\":\"Python engine not reachable\"}";

        var pathParts = stemsPath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
        if (pathParts.Length < 2)
            return "{\"status\":\"error\",\"message\":\"Invalid stemsPath\"}";

        var folderName = pathParts.Last();
        var model = pathParts.Take(pathParts.Length -1).Last();

        var response = await _client.GetAsync($"analysis-data/{model}/{folderName}");
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetAnalysisAsync(string stemsPath)
    {
        if (!await _manager.EnsureRunningAsync())
            return "{\"status\":\"error\",\"message\":\"Python engine not reachable\"}";

        var pathParts = stemsPath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
        if (pathParts.Length < 2)
            return "{\"status\":\"error\",\"message\":\"Invalid stemsPath\"}";

        var folderName = pathParts.Last();
        var model = pathParts.Take(pathParts.Length -1).Last();

        var response = await _client.GetAsync($"analysis-data/{model}/{folderName}");
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> SeparateStemsAsync(IFormFile? file, string stems, string? originalFilePath)
    {
        if (!await _manager.EnsureRunningAsync())
            return "{\"status\":\"error\",\"message\":\"Python engine not reachable\"}";

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "separate-stems");

        if (file != null)
        {
            var multipartContent = await BuildMultipartAsync(file);
            // Add stems as a string content
            multipartContent.Add(new StringContent(stems), "request.stems");
            // Add originalFilePath if present
            if (!string.IsNullOrEmpty(originalFilePath))
            {
                multipartContent.Add(new StringContent(originalFilePath), "request.file_path");
            }
            requestMessage.Content = multipartContent;
        }
        else if (!string.IsNullOrEmpty(originalFilePath))
        {
            // If no file is provided, send JSON with file_path and stems
            var payload = new { file_path = originalFilePath, stems = stems };
            requestMessage.Content = JsonContent.Create(payload);
        }
        else
        {
            return "{\"status\":\"error\",\"message\":\"No file or file path provided for stem separation.\"}";
        }

        var response = await _client.SendAsync(requestMessage);
        return await response.Content.ReadAsStringAsync();
    }


    private static async Task<MultipartFormDataContent> BuildMultipartAsync(IFormFile file)
    {
        var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        content.Add(new ByteArrayContent(ms.ToArray()), "file", file.FileName);
        return content;
    }
}
