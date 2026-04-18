using System;

namespace AiMusicWorkstation.Desktop.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public PlaybackViewModel Playback { get; }
    public LibraryViewModel Library { get; }

    public MainWindowViewModel(PlaybackViewModel playback, LibraryViewModel library)
    {
        Playback = playback ?? throw new ArgumentNullException(nameof(playback));
        Library = library ?? throw new ArgumentNullException(nameof(library));
    }
}
