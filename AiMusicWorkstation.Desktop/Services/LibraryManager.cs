using System.IO;
using System.Text.Json;
using AiMusicWorkstation.Shared.Models;
using AiMusicWorkstation.Domain.Repositories;

namespace AiMusicWorkstation.Desktop.Services
{
    public class LibraryManager : ILibraryRepository
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

        public Task<List<SongProject>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Projects.ToList());

        public Task<SongProject?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(Projects.FirstOrDefault(p => p.Id == id));

        public Task AddAsync(SongProject project, CancellationToken cancellationToken = default)
        {
            Projects.RemoveAll(p => p.Id == project.Id);
            Projects.Insert(0, project);
            SaveLibrary();
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SongProject project, CancellationToken cancellationToken = default)
        {
            var index = Projects.FindIndex(p => p.Id == project.Id);
            if (index >= 0)
                Projects[index] = project;
            else
                Projects.Insert(0, project);
            SaveLibrary();
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            Projects.RemoveAll(p => p.Id == id);
            SaveLibrary();
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            SaveLibrary();
            return Task.CompletedTask;
        }
    }
}