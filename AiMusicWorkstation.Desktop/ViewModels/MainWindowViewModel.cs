namespace AiMusicWorkstation.Desktop.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public PlaybackViewModel Playback { get; }
    public LibraryViewModel Library { get; }

    public MainWindowViewModel(PlaybackViewModel playbackViewModel, LibraryViewModel libraryViewModel)
    {
        Playback = playbackViewModel ?? throw new ArgumentNullException(nameof(playbackViewModel));
        Library = libraryViewModel ?? throw new ArgumentNullException(nameof(libraryViewModel));
    }
}
