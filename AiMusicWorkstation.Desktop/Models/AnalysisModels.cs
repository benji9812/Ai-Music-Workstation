using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AiMusicWorkstation.Desktop.Models
{
    public class AnalysisResult
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("bpm")]
        public double Bpm { get; set; }

        [JsonPropertyName("key")]
        public string Key { get; set; }

        // VIKTIGT: Mappar Python 'stems_path' till C# 'StemsPath'
        [JsonPropertyName("stems_path")]
        public string StemsPath { get; set; }

        [JsonPropertyName("original_path")]
        public string OriginalPath { get; set; }

        [JsonPropertyName("lyrics")]
        public List<LyricSegment> Lyrics { get; set; }

        [JsonPropertyName("chords")]
        public List<ChordEvent> Chords { get; set; }
    }

    public class LyricSegment : INotifyPropertyChanged
    {
        [JsonPropertyName("start")]
        public double Start { get; set; }

        [JsonPropertyName("end")]
        public double End { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

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