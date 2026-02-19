using SpotifyAPI.Web;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;
using Microsoft.Extensions.Configuration;

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
            _spotifyClientId = config.GetValue<string>("Spotify:ClientId");
            _spotifyClientSecret = config.GetValue<string>("Spotify:ClientSecret");
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

        public async Task<OfficialMetadata> GetOfficialMetadata(string trackId)
        {
            if (string.IsNullOrEmpty(trackId)) return new OfficialMetadata();
            try
            {
                var spotify = await GetSpotifyClient();
                var analysis = await spotify.Tracks.GetAudioFeatures(trackId);

                string[] notes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "Bb", "B" };
                string keyStr = analysis.Key >= 0 ? notes[analysis.Key] : "Unknown";
                if (analysis.Mode == 0 && keyStr != "Unknown") keyStr += "m";

                return new OfficialMetadata { Bpm = analysis.Tempo, Key = keyStr };
            }
            catch { return new OfficialMetadata(); }
        }

        private async Task<SpotifyClient> GetSpotifyClient()
        {
            var config = SpotifyClientConfig.CreateDefault();
            var request = new ClientCredentialsRequest(_spotifyClientId, _spotifyClientSecret);
            var response = await new OAuthClient(config).RequestToken(request);
            return new SpotifyClient(config.WithToken(response.AccessToken));
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
    }
}