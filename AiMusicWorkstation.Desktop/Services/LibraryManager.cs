using System.IO;
using System.Text.Json;
using AiMusicWorkstation.Shared.Models;

namespace AiMusicWorkstation.Desktop.Services
{
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

                    foreach (var p in Projects.Where(p => p.KeySource == KeySource.Unknown))
                        p.KeySource = p.IsOfficialData ? KeySource.Metadata : KeySource.Generated;

                    SaveLibrary();
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