using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace AiMusicWorkstation.Desktop.Services
{
    public class SongProject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; }
        public string Artist { get; set; } = "Unknown Artist";
        public double Bpm { get; set; }
        public string Key { get; set; }
        public string StemsPath { get; set; }

        public TimeSpan Duration { get; set; }
        public string Genre { get; set; } = "Uncategorized";

        // NYHET: Mapp-namn (Om den är tom hamnar den i "General")
        public string GroupName { get; set; } = "General";

        public DateTime DateAdded { get; set; } = DateTime.Now;
        public string DurationDisplay => Duration.ToString(@"mm\:ss");
        public bool IsOfficialData { get; set; } // <--- KRÄVS FÖR ATT SPARA STATUSEN

    }

    public class LibraryManager
    {
        private string _libraryFile = "library.json";
        public List<SongProject> Projects { get; private set; } = new List<SongProject>();

        public LibraryManager()
        {
            LoadLibrary();
        }

        public void LoadLibrary()
        {
            if (File.Exists(_libraryFile))
            {
                try
                {
                    string json = File.ReadAllText(_libraryFile);
                    Projects = JsonSerializer.Deserialize<List<SongProject>>(json) ?? new List<SongProject>();
                }
                catch
                {
                    Projects = new List<SongProject>();
                }
            }
        }

        public void SaveLibrary()
        {
            string json = JsonSerializer.Serialize(Projects, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_libraryFile, json);
        }

        public void AddProject(SongProject project)
        {
            Projects.RemoveAll(p => p.StemsPath == project.StemsPath);
            Projects.Insert(0, project);
            SaveLibrary();
        }
    }
}