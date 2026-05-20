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

    public async Task<string> GetJobStatusAsync(string jobId)
    {
        if (!await _manager.EnsureRunningAsync())
            return "{\"status\":\"error\",\"message\":\"Python engine not reachable\"}";
        var response = await _client.GetAsync($"job-status/{jobId}");
        return await response.Content.ReadAsStringAsync();
    }

    public Task<bool> IsHealthyAsync() => _manager.IsHealthyAsync();

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
