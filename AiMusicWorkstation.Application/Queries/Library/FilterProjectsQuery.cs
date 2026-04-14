using AiMusicWorkstation.Application.Queries;

namespace AiMusicWorkstation.Application.Queries.Library;

public class FilterProjectsQuery : IQuery<List<SongProjectDto>>
{
    public string SearchTerm { get; set; } = string.Empty;
    public string SelectedGenre { get; set; } = string.Empty;
    public string SortBy { get; set; } = "Latest"; // "Latest", "A-Z", "BPM"
}

public class SongProjectDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public int Bpm { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
}
