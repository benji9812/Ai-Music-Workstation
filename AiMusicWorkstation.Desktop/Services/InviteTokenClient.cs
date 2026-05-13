using AiMusicWorkstation.Shared.Models;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Json;

namespace AiMusicWorkstation.Desktop.Services;

public class InviteTokenClient
{
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };
    private readonly Uri _baseUri;

    public InviteTokenClient(IConfiguration configuration)
    {
        string baseUrl = configuration["Api:BaseUrl"] ?? "https://localhost:7107/";
        _baseUri = new Uri(baseUrl);
    }

    public async Task<InviteTokenValidationResponse?> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        var request = new InviteTokenValidationRequest(token);
        using var response = await SharedClient.PostAsJsonAsync(
            new Uri(_baseUri, "api/auth/validate-invite"),
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<InviteTokenValidationResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<bool> ValidateSessionAsync(string sessionTicket, CancellationToken cancellationToken = default)
    {
        var request = new InviteSessionValidationRequest(sessionTicket);
        using var response = await SharedClient.PostAsJsonAsync(
            new Uri(_baseUri, "api/auth/validate-session"),
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var result = await response.Content.ReadFromJsonAsync<InviteSessionValidationResponse>(
            cancellationToken: cancellationToken);
        return result?.IsValid == true;
    }
}
