using AiMusicWorkstation.Shared.Models;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Json;

namespace AiMusicWorkstation.Desktop.Services;

public class InviteTokenClient : IDisposable
{
    private readonly HttpClient _httpClient;

    public InviteTokenClient(IConfiguration configuration)
    {
        string baseUrl = configuration["Api:BaseUrl"] ?? "https://localhost:7107/";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public async Task<bool> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        var request = new InviteTokenValidationRequest(token);
        using var response = await _httpClient.PostAsJsonAsync(
            "api/auth/validate-invite",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var result = await response.Content.ReadFromJsonAsync<InviteTokenValidationResponse>(
            cancellationToken: cancellationToken);
        return result?.IsValid == true;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
