using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AiMusicWorkstation.Shared.Models
{
    public class AnalysisResult
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("bpm")]
        public double Bpm { get; set; }

        [JsonPropertyName("bpm_source")]
        public string BpmSource { get; set; } = "analysis";

        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("key_source")]
        public string KeySource { get; set; } = "analysis";

        [JsonPropertyName("stems_path")]
        public string StemsPath { get; set; }

        [JsonPropertyName("original_path")]
        public string OriginalPath { get; set; }

        [JsonPropertyName("lyrics")]
        public List<LyricSegment> Lyrics { get; set; } = new();

        [JsonPropertyName("chords")]
        public List<ChordEvent> Chords { get; set; } = new();

        [JsonPropertyName("time_signature")]
        public int TimeSignature { get; set; } = 4;

        [JsonPropertyName("time_sig_source")]
        public string TimeSigSource { get; set; } = "analysis";
    }

    public class LyricSegment : INotifyPropertyChanged
    {
        [JsonPropertyName("start")]
        public double Start { get; set; }

        [JsonPropertyName("end")]
        public double End { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        // ← Lägg till denna
        public double FontSize { get; set; } = 14;

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value) return;
                _isActive = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class ChordEvent
    {
        [JsonPropertyName("time")]
        public double Time { get; set; }

        [JsonPropertyName("chord")]
        public string Chord { get; set; }
    }
}
