namespace AiMusicWorkstation.Application.Commands.Library;

public class ImportSongCommand : ICommand
{
    public string FilePath { get; set; } = string.Empty;
    public string? SpotifyUrl { get; set; }
    public string? CustomTitle { get; set; }
    public string? ArtistName { get; set; }
}
