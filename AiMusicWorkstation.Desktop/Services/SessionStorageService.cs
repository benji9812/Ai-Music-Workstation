using AiMusicWorkstation.Desktop.Models;
using AiMusicWorkstation.Shared.Models;
using System.IO;
using System.Text.Json;

namespace AiMusicWorkstation.Desktop.Services;

public class SessionStorageService
{
    public void SaveSession(
        string stemsPath,
        List<LyricSegment> lyrics,
        List<ChordEvent> chords,
        List<SongSection>? sections = null)
    {
        try
        {
            string dir = Directory.Exists(stemsPath) ? stemsPath : Path.GetDirectoryName(stemsPath) ?? string.Empty;
            if (string.IsNullOrEmpty(dir)) return;

            var sectionDtos = (sections ?? new List<SongSection>()).Select(s => new
            {
                label = s.Label,
                start = s.StartTime,
                end = s.EndTime,
                color = s.Color
            }).ToList();

            var data = new { lyrics, chords, sections = sectionDtos };
            var opts = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(data, opts);
            File.WriteAllText(Path.Combine(dir, "session.json"), json);
        }
        catch
        {
        }
    }

    public (List<LyricSegment> lyrics, List<ChordEvent> chords, List<SongSection> sections) LoadSession(string stemsPath)
    {
        try
        {
            string dir = Directory.Exists(stemsPath) ? stemsPath : Path.GetDirectoryName(stemsPath) ?? string.Empty;
            if (string.IsNullOrEmpty(dir)) return (new List<LyricSegment>(), new List<ChordEvent>(), new List<SongSection>());

            string file = Path.Combine(dir, "session.json");
            if (!File.Exists(file))
                return (new List<LyricSegment>(), new List<ChordEvent>(), new List<SongSection>());

            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var lyrics = JsonSerializer.Deserialize<List<LyricSegment>>(
                doc.RootElement.GetProperty("lyrics").GetRawText()) ?? new List<LyricSegment>();
            var chords = JsonSerializer.Deserialize<List<ChordEvent>>(
                doc.RootElement.GetProperty("chords").GetRawText()) ?? new List<ChordEvent>();

            List<SongSection> sections = new List<SongSection>();
            if (doc.RootElement.TryGetProperty("sections", out var sectionsEl))
                sections = JsonSerializer.Deserialize<List<SongSection>>(sectionsEl.GetRawText())
                           ?? new List<SongSection>();

            return (lyrics, chords, sections);
        }
        catch
        {
            return (new List<LyricSegment>(), new List<ChordEvent>(), new List<SongSection>());
        }
    }
}
