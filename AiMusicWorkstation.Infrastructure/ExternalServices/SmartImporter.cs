using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

namespace AiMusicWorkstation.Infrastructure.ExternalServices
{
    public class SongImportResult
    {
        public string FilePath { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string SpotifyId { get; set; }
    }

    public class OfficialMetadata
    {
        public string Genre { get; set; } = "Uncategorized";
    }

    public class SmartImporter
    {
        private readonly string _spotifyClientId;
        private readonly string _spotifyClientSecret;
        private readonly YoutubeClient _youtube;

        // ✅ Bug 1 fix — en statisk HttpClient med User-Agent satt i konstruktorn
        private static readonly HttpClient _httpClient = new HttpClient();

        public SmartImporter(IConfiguration config)
        {
            _youtube = new YoutubeClient();
            _spotifyClientId = config["Spotify:ClientId"];
            _spotifyClientSecret = config["Spotify:ClientSecret"];

            // ✅ Sätt User-Agent en gång — MusicBrainz kräver detta
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
                _httpClient.DefaultRequestHeaders.Add(
                    "User-Agent", "AiMusicWorkstation/1.0 (Benjiw98@gmail.com)");
        }

        public async Task<SongImportResult> DownloadSongAsync(string url, IProgress<string> statusReporter)
        {
            string videoUrl = url;
            string finalTitle = "";
            string finalArtist = "Unknown Artist";
            string spotifyId = null;

            if (url.ToLower().Contains("spotify.com") && url.Contains("/track/"))
            {
                statusReporter.Report("Reading Spotify Metadata...");
                var trackInfo = await GetSpotifyTrackInfo(url);

                if (trackInfo != null)
                {
                    finalArtist = trackInfo.Value.Artist;
                    finalTitle = trackInfo.Value.Title;
                    spotifyId = ExtractSpotifyId(url);

                    string query = $"{finalArtist} - {finalTitle} audio";
                    statusReporter.Report($"Searching YouTube: {query}");

                    var searchResults = await _youtube.Search.GetVideosAsync(query);
                    var bestMatch = searchResults.FirstOrDefault();

                    if (bestMatch == null) throw new Exception("Song not found on YouTube.");
                    videoUrl = bestMatch.Url;
                }
            }

            var video = await _youtube.Videos.GetAsync(videoUrl);
            if (string.IsNullOrEmpty(finalTitle))
            {
                finalTitle = video.Title;
                finalArtist = video.Author.ChannelTitle;
            }

            var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(video.Id);
            var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            if (streamInfo == null) throw new Exception("No audio stream available.");

            string downloadFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
            Directory.CreateDirectory(downloadFolder);

            string cleanFileName = Regex.Replace(
                $"{finalArtist} - {finalTitle}", @"[^a-zA-Z0-9\s-]", "").Trim();
            string ext = streamInfo.Container.Name;
            string filePath = Path.Combine(downloadFolder, $"{cleanFileName}.{ext}");

            statusReporter.Report($"Downloading: {finalTitle}...");
            await _youtube.Videos.Streams.DownloadAsync(streamInfo, filePath);

            return new SongImportResult
            {
                FilePath = filePath,
                Title = finalTitle,
                Artist = finalArtist,
                SpotifyId = spotifyId
            };
        }

        public string ExtractSpotifyId(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            try
            {
                var match = Regex.Match(url, @"track/([a-zA-Z0-9]{22})");
                if (match.Success) return match.Groups[1].Value;
                var cleanUrl = url.Split('?')[0];
                return cleanUrl.Split('/').Last();
            }
            catch { return null; }
        }

        private async Task<(string Artist, string Title)?> GetSpotifyTrackInfo(string url)
        {
            try
            {
                string id = ExtractSpotifyId(url);
                if (string.IsNullOrEmpty(id)) return null;

                if (!string.IsNullOrEmpty(_spotifyClientId) && !string.IsNullOrEmpty(_spotifyClientSecret))
                {
                    try
                    {
                        var spotify = await GetSpotifyClient();
                        var track = await spotify.Tracks.Get(id);
                        return (track.Artists[0].Name, track.Name);
                    }
                    catch
                    {
                        // Fallback to scraping
                    }
                }

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                    var html = await client.GetStringAsync($"https://open.spotify.com/track/{id}");
                    var titleMatch = Regex.Match(html, @"<title>(.*?)</title>", RegexOptions.IgnoreCase);
                    if (titleMatch.Success)
                    {
                        string pageTitle = System.Net.WebUtility.HtmlDecode(titleMatch.Groups[1].Value);
                        var match = Regex.Match(pageTitle, @"^(.*?) - (?:song and lyrics|song|single|EP|album) by (.*?) \| Spotify$", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            return (match.Groups[2].Value.Trim(), match.Groups[1].Value.Trim());
                        }
                    }

                    // Fallback to OpenGraph metadata
                    var ogTitleMatch = Regex.Match(html, @"<meta[^>]+property=[""']og:title[""'][^>]+content=[""'](.*?)[""']", RegexOptions.IgnoreCase);
                    if (!ogTitleMatch.Success)
                    {
                        ogTitleMatch = Regex.Match(html, @"<meta[^>]+content=[""'](.*?)[""'][^>]+property=[""']og:title[""']", RegexOptions.IgnoreCase);
                    }

                    var ogDescMatch = Regex.Match(html, @"<meta[^>]+property=[""']og:description[""'][^>]+content=[""'](.*?)[""']", RegexOptions.IgnoreCase);
                    if (!ogDescMatch.Success)
                    {
                        ogDescMatch = Regex.Match(html, @"<meta[^>]+content=[""'](.*?)[""'][^>]+property=[""']og:description[""']", RegexOptions.IgnoreCase);
                    }

                    if (ogTitleMatch.Success && ogDescMatch.Success)
                    {
                        string ogTitle = System.Net.WebUtility.HtmlDecode(ogTitleMatch.Groups[1].Value).Trim();
                        string ogDesc = System.Net.WebUtility.HtmlDecode(ogDescMatch.Groups[1].Value).Trim();
                        var parts = ogDesc.Split(new[] { " · " }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 1)
                        {
                            return (parts[0].Trim(), ogTitle);
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public async Task<OfficialMetadata> GetOfficialMetadata(string trackId)
        {
            if (string.IsNullOrEmpty(trackId)) return new OfficialMetadata();
            try
            {
                string artist = "";
                string title = "";

                if (!string.IsNullOrEmpty(_spotifyClientId) && !string.IsNullOrEmpty(_spotifyClientSecret))
                {
                    try
                    {
                        var spotify = await GetSpotifyClient();
                        var track = await spotify.Tracks.Get(trackId);
                        artist = track.Artists?[0]?.Name ?? "";
                        title = track.Name ?? "";
                    }
                    catch
                    {
                        // Fallback to scrape below
                    }
                }

                if (string.IsNullOrEmpty(artist) || string.IsNullOrEmpty(title))
                {
                    var scraped = await GetSpotifyTrackInfo($"https://open.spotify.com/track/{trackId}");
                    if (scraped != null)
                    {
                        artist = scraped.Value.Artist;
                        title = scraped.Value.Title;
                    }
                }

                if (!string.IsNullOrEmpty(artist))
                {
                    string genre = await GetGenreFromMusicBrainz(artist, title);
                    return new OfficialMetadata { Genre = genre };
                }
            }
            catch { }
            return new OfficialMetadata();
        }

        // ✅ Bug 2 fix — title-parameter tillagd
        public async Task<string> GetGenreFromMusicBrainz(string artistName, string title = "")
        {
            if (string.IsNullOrWhiteSpace(artistName)) return "Uncategorized";
            try
            {
                // Sök artist
                string query = Uri.EscapeDataString(artistName);
                string searchUrl = $"https://musicbrainz.org/ws/2/artist/?query=artist:{query}&fmt=json&limit=1";
                var searchResp = await _httpClient.GetStringAsync(searchUrl);
                var searchJson = JsonDocument.Parse(searchResp);

                var artists = searchJson.RootElement.GetProperty("artists");
                if (artists.GetArrayLength() == 0) return "Uncategorized";

                string mbid = artists[0].GetProperty("id").GetString() ?? "";
                if (string.IsNullOrEmpty(mbid)) return "Uncategorized";

                // ✅ Bug 3 fix — rate limit: vänta 1.1 sekunder mellan requests
                await Task.Delay(1100);

                // Hämta genres via MBID
                string genreUrl = $"https://musicbrainz.org/ws/2/artist/{mbid}?inc=genres&fmt=json";
                var genreResp = await _httpClient.GetStringAsync(genreUrl);
                var genreJson = JsonDocument.Parse(genreResp);

                var genres = genreJson.RootElement.GetProperty("genres");
                if (genres.GetArrayLength() == 0) return "Uncategorized";

                string best = genres
                    .EnumerateArray()
                    .OrderByDescending(g => g.GetProperty("count").GetInt32())
                    .First()
                    .GetProperty("name")
                    .GetString() ?? "Uncategorized";

                return char.ToUpper(best[0]) + best[1..];
            }
            catch { return "Uncategorized"; }
        }

        private SpotifyClient _spotifyClient;
        private DateTime _tokenExpiry = DateTime.MinValue;

        private async Task<SpotifyClient> GetSpotifyClient()
        {
            if (_spotifyClient != null && DateTime.Now < _tokenExpiry)
                return _spotifyClient;

            var config = SpotifyClientConfig.CreateDefault();
            var request = new ClientCredentialsRequest(_spotifyClientId, _spotifyClientSecret);
            var response = await new OAuthClient(config).RequestToken(request);
            _spotifyClient = new SpotifyClient(config.WithToken(response.AccessToken));
            _tokenExpiry = DateTime.Now.AddSeconds(response.ExpiresIn - 30);
            return _spotifyClient;
        }
    }
}