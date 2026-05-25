using AiMusicWorkstation.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace AiMusicWorkstation.Infrastructure.ExternalServices;

public class LyricsService : ILyricsService
{
    private readonly HttpClient _httpClient;
    private readonly string _geniusAccessToken;
    private readonly ILogger<LyricsService> _logger;

    public LyricsService(HttpClient httpClient, IConfiguration config, ILogger<LyricsService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _geniusAccessToken = config["Genius:AccessToken"] ?? "";
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AiMusicWorkstation/1.0");
        }
    }

    public async Task<LyricsResult?> GetLyricsAsync(string artist, string title, string? album = null, double? durationSeconds = null)
    {
        // 1. Try LRCLib (Primary)
        var lrcResult = await GetFromLRCLibAsync(artist, title, album, durationSeconds);
        if (lrcResult != null)
        {
            return lrcResult;
        }

        // 2. Try Genius (Fallback)
        if (!string.IsNullOrEmpty(_geniusAccessToken))
        {
            return await GetFromGeniusAsync(artist, title);
        }

        return null;
    }

    private async Task<LyricsResult?> GetFromLRCLibAsync(string artist, string title, string? album, double? durationSeconds)
    {
        try
        {
            _logger.LogInformation("Fetching lyrics from LRCLib for {Artist} - {Title}", artist, title);

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["artist_name"] = artist;
            query["track_name"] = title;
            if (!string.IsNullOrEmpty(album)) query["album_name"] = album;
            if (durationSeconds.HasValue) query["duration"] = ((int)durationSeconds.Value).ToString();

            var url = $"https://lrclib.net/api/get?{query}";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<JsonElement>();
                return new LyricsResult
                {
                    PlainLyrics = data.TryGetProperty("plainLyrics", out var p) ? p.GetString() ?? "" : "",
                    SyncedLyrics = data.TryGetProperty("syncedLyrics", out var s) ? s.GetString() : null,
                    Source = LyricsSource.LRCLib
                };
            }

            _logger.LogWarning("LRCLib returned status code {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching lyrics from LRCLib");
        }

        return null;
    }

    private async Task<LyricsResult?> GetFromGeniusAsync(string artist, string title)
    {
        try
        {
            _logger.LogInformation("Fetching lyrics from Genius for {Artist} - {Title}", artist, title);

            // 1. Search for the song
            var searchQuery = $"{artist} {title}";
            var searchUrl = $"https://api.genius.com/search?q={Uri.EscapeDataString(searchQuery)}";

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _geniusAccessToken);
            var searchResponse = await _httpClient.GetAsync(searchUrl);
            _httpClient.DefaultRequestHeaders.Authorization = null;

            if (!searchResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Genius API search failed with status {StatusCode}", searchResponse.StatusCode);
                return null;
            }

            var searchData = await searchResponse.Content.ReadFromJsonAsync<JsonElement>();
            var hits = searchData.GetProperty("response").GetProperty("hits");

            if (hits.GetArrayLength() == 0)
            {
                _logger.LogInformation("No hits found on Genius for {Artist} - {Title}", artist, title);
                return null;
            }

            // Take the first hit
            var songUrl = hits[0].GetProperty("result").GetProperty("url").GetString();
            if (string.IsNullOrEmpty(songUrl)) return null;

            // 2. Scrape the lyrics from the URL
            // Note: Genius API doesn't provide lyrics directly. We must scrape the page.
            _logger.LogInformation("Scraping Genius lyrics from {Url}", songUrl);
            var htmlResponse = await _httpClient.GetStringAsync(songUrl);

            var lyrics = ExtractLyricsFromGeniusHtml(htmlResponse);
            if (!string.IsNullOrEmpty(lyrics))
            {
                return new LyricsResult
                {
                    PlainLyrics = lyrics,
                    SyncedLyrics = null,
                    Source = LyricsSource.Genius
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching lyrics from Genius");
        }

        return null;
    }

    private string ExtractLyricsFromGeniusHtml(string html)
    {
        // Genius uses several different container classes for lyrics depending on the version of the page.
        // Common ones are 'Lyrics__Container' or 'lyrics'.

        // This is a simplified scraper logic.
        var lyrics = "";

        // Modern Genius (Lyrics__Container)
        var matches = Regex.Matches(html, @"<div[^>]*class=""Lyrics__Container[^""]*""[^>]*>(.*?)<\/div>", RegexOptions.Singleline);
        if (matches.Count > 0)
        {
            foreach (Match match in matches)
            {
                lyrics += match.Groups[1].Value;
            }
        }
        else
        {
            // Older Genius (lyrics class)
            var oldMatch = Regex.Match(html, @"<div[^>]*class=""lyrics""[^>]*>(.*?)<\/div>", RegexOptions.Singleline);
            if (oldMatch.Success)
            {
                lyrics = oldMatch.Groups[1].Value;
            }
        }

        if (string.IsNullOrEmpty(lyrics)) return "";

        // Basic HTML cleanup
        lyrics = Regex.Replace(lyrics, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        lyrics = Regex.Replace(lyrics, @"<[^>]*>", "", RegexOptions.IgnoreCase);
        lyrics = HttpUtility.HtmlDecode(lyrics).Trim();

        return lyrics;
    }
}
