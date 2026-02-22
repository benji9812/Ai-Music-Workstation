using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

namespace AiMusicWorkstation.Desktop.Services
{
    public class SmartImporter
    {
        private readonly string _spotifyClientId;
        private readonly string _spotifyClientSecret;

        private readonly YoutubeClient _youtube;

        public SmartImporter(IConfiguration config)
        {
            _youtube = new YoutubeClient();
            _spotifyClientId = config["Spotify:ClientId"];
            _spotifyClientSecret = config["Spotify:ClientSecret"];
        }

        public async Task<SongImportResult> DownloadSongAsync(string url, IProgress<string> statusReporter)
        {
            string videoUrl = url;
            string finalTitle = "";
            string finalArtist = "Unknown Artist";
            string spotifyId = null;

            // 1. SMARTARE SPOTIFY CHECK (UPPDATERAD)
            // Nu kollar vi om det är ENHETLIGT en Spotify-länk (oavsett om det är via Google eller direkt)
            // Kravet är att den innehåller "spotify.com" och "/track/"
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

            // 2. YOUTUBE INFO
            var video = await _youtube.Videos.GetAsync(videoUrl);
            if (string.IsNullOrEmpty(finalTitle))
            {
                finalTitle = video.Title;
                finalArtist = video.Author.ChannelTitle;
            }

            // 3. HÄMTA LJUDSTRÖM
            var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(video.Id);
            var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

            if (streamInfo == null) throw new Exception("No audio stream available.");

            // 4. FILHANTERING
            string downloadFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
            Directory.CreateDirectory(downloadFolder);

            // Rensa filnamn
            string cleanFileName = Regex.Replace($"{finalArtist} - {finalTitle}", @"[^a-zA-Z0-9\s-]", "").Trim();
            string ext = streamInfo.Container.Name;
            string filePath = Path.Combine(downloadFolder, $"{cleanFileName}.{ext}");

            // 5. DOWNLOAD
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
                // Regex som hittar 22 tecken (standard Spotify ID) efter /track/
                var match = Regex.Match(url, @"track/([a-zA-Z0-9]{22})");
                if (match.Success) return match.Groups[1].Value;

                // Fallback: Försök hitta sista delen av URL:en om regex missar
                var cleanUrl = url.Split('?')[0]; // Ta bort query params
                var parts = cleanUrl.Split('/');
                return parts.Last();
            }
            catch { return null; }
        }

        private async Task<(string Artist, string Title)?> GetSpotifyTrackInfo(string url)
        {
            try
            {
                string id = ExtractSpotifyId(url);
                if (string.IsNullOrEmpty(id)) return null;

                var spotify = await GetSpotifyClient();
                var track = await spotify.Tracks.Get(id);
                return (track.Artists[0].Name, track.Name);
            }
            catch { return null; }
        }

        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<OfficialMetadata> GetOfficialMetadata(string trackId)
        {
            if (string.IsNullOrEmpty(trackId)) return new OfficialMetadata();

            try
            {
                // 1. Hämta artistnamn från Spotify
                var spotify = await GetSpotifyClient();
                var track = await spotify.Tracks.Get(trackId);
                string artistName = track.Artists?[0]?.Name ?? "";

                // 2. Hämta genre från MusicBrainz
                string genre = await GetGenreFromMusicBrainz(artistName);

                return new OfficialMetadata { Genre = genre };
            }
            catch
            {
                return new OfficialMetadata();
            }
        }

        private async Task<string> GetGenreFromMusicBrainz(string artistName)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                // MusicBrainz kräver en User-Agent
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "MusicManagerApp/1.0 (Benjiw98@gmail.com)");

                string searchUrl = $"https://musicbrainz.org/ws/2/artist/?query=artist:{Uri.EscapeDataString(artistName)}&fmt=json&limit=1";
                var searchResponse = await _httpClient.GetStringAsync(searchUrl);
                var searchJson = JsonDocument.Parse(searchResponse);

                var artists = searchJson.RootElement.GetProperty("artists");
                if (artists.GetArrayLength() == 0) return "Uncategorized";

                string mbid = artists[0].GetProperty("id").GetString() ?? "";
                if (string.IsNullOrEmpty(mbid)) return "Uncategorized";

                // 3. Hämta genres via artist MBID
                string genreUrl = $"https://musicbrainz.org/ws/2/artist/{mbid}?inc=genres&fmt=json";
                var genreResponse = await _httpClient.GetStringAsync(genreUrl);
                var genreJson = JsonDocument.Parse(genreResponse);

                var genres = genreJson.RootElement.GetProperty("genres");
                if (genres.GetArrayLength() == 0) return "Uncategorized";

                // Välj genre med högst count (mest röstade)
                string bestGenre = genres
                    .EnumerateArray()
                    .OrderByDescending(g => g.GetProperty("count").GetInt32())
                    .First()
                    .GetProperty("name")
                    .GetString() ?? "Uncategorized";

                return char.ToUpper(bestGenre[0]) + bestGenre.Substring(1);
            }
            catch
            {
                return "Uncategorized";
            }
        }

        public async Task<string> SearchSpotifyTrackId(string title, string artist)
        {
            try
            {
                var spotify = await GetSpotifyClient();
                var results = await spotify.Search.Item(new SearchRequest(
                    SearchRequest.Types.Track, $"{title} {artist}"));

                var track = results.Tracks?.Items?.FirstOrDefault();
                return track?.Id;
            }
            catch { return null; }
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

    public class SongImportResult
    {
        public string FilePath { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string SpotifyId { get; set; }
    }

    public class OfficialMetadata
    {
        public double? Bpm { get; set; }
        public string Key { get; set; }
        public string Genre { get; set; } = "Uncategorized"; // NY RAD
    }
}