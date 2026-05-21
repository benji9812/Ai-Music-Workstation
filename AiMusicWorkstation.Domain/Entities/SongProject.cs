
namespace AiMusicWorkstation.Domain.Entities
{
    public enum KeySource
    {
        Unknown,
        Metadata,
        Generated
    }

    public class SongProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = "Unknown Artist";
        public double Bpm { get; set; }
        public string Key { get; set; } = string.Empty;
        public string StemsPath { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = string.Empty;
        public string SpotifyId { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string Genre { get; set; } = "Uncategorized";
        public string GroupName { get; set; } = "General";
        public DateTime DateAdded { get; set; } = DateTime.Now;
        public string DurationDisplay => Duration.ToString(@"mm\:ss");
        public bool IsOfficialData { get; set; }
        public KeySource KeySource { get; set; } = KeySource.Unknown;
        public int TimeSignature { get; set; } = 4;
    }
}
