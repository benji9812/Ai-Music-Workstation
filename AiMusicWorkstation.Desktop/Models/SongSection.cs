using System.ComponentModel;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AiMusicWorkstation.Desktop.Models
{
    public class SongSection : INotifyPropertyChanged
    {
        private bool _isActive;


        [JsonPropertyName("label")]
        public string Label { get; set; } = "";

        [JsonPropertyName("start")]
        public double StartTime { get; set; }

        [JsonPropertyName("end")]
        public double EndTime { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; } = "#007ACC";
        
        [JsonIgnore]
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(nameof(IsActive)); }
        }

        [JsonIgnore]
        public string TimeDisplay =>
        System.TimeSpan.FromSeconds(StartTime).ToString(@"m\:ss");

        [JsonIgnore]
        public System.Windows.Media.SolidColorBrush ColorBrush =>
        new System.Windows.Media.SolidColorBrush(
        (System.Windows.Media.Color)System.Windows.Media.ColorConverter
        .ConvertFromString(Color));

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public static class SectionColors
    {
        public static string Get(string label)
        {
            var map = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "Intro",        "#4A4A8A" },
                { "Verse",        "#1A6B3A" },
                { "Pre",          "#6B4A1A" },
                { "Chorus",       "#8B1A1A" },
                { "Bridge",       "#6B1A6B" },
                { "Solo",         "#8B6B00" },
                { "Instrumental", "#1A4A6B" },
                { "Outro",        "#2A4A6B" },
                { "Break",        "#3A3A3A" },
            };
            foreach (var kv in map)
                if (label.StartsWith(kv.Key, System.StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
            return "#333355";
        }
    }
}
