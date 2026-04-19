using Microsoft.AspNetCore.Http;
using System.Net.Http.Json;

namespace AiMusicWorkstation.Api.Services;

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
        await _manager.EnsureRunningAsync();
        using var content = await BuildMultipartAsync(file);
        var response = await _client.PostAsync("analyze", content);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> ReAnalyzeAsync(IFormFile file)
    {
        await _manager.EnsureRunningAsync();
        using var content = await BuildMultipartAsync(file);
        var response = await _client.PostAsync("analyze-only", content);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetStructureAsync(object payload)
    {
        await _manager.EnsureRunningAsync();
        var response = await _client.PostAsJsonAsync("structure", payload);
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
