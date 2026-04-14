using AiMusicWorkstation.Domain.Services;

namespace AiMusicWorkstation.Desktop.Services;

/// <summary>
/// Adapter to make StemPlayer compatible with IAudioPlayer
/// </summary>
public class AudioPlayerAdapter : IAudioPlayer
{
    private readonly StemPlayer _stemPlayer;
    
    public TimeSpan CurrentTime
    {
        get => _stemPlayer.CurrentTime;
        set => _stemPlayer.CurrentTime = value;
    }
    
    public TimeSpan TotalTime => _stemPlayer.TotalTime;
    public bool IsPlaying => _stemPlayer.IsPlaying;
    public int SemitoneShift
    {
        get => _stemPlayer.SemitoneShift;
        set => _stemPlayer.SemitoneShift = value;
    }
    
    public AudioPlayerAdapter(StemPlayer stemPlayer)
    {
        _stemPlayer = stemPlayer ?? throw new ArgumentNullException(nameof(stemPlayer));
    }
    
    public void LoadStems(string stemsPath) => _stemPlayer.LoadStems(stemsPath);
    public void Play() => _stemPlayer.Play();
    public void Pause() => _stemPlayer.Pause();
    public void Stop() => _stemPlayer.Stop();
    public void SetMasterVolume(float volume) => _stemPlayer.SetMasterVolume(volume);
    public void SetVolume(string stemName, float volume) => _stemPlayer.SetVolume(stemName, volume);
    public void SetMute(string stemName, bool isMuted) => _stemPlayer.SetMute(stemName, isMuted);
    public void SetSolo(string stemName, bool isSolo) => _stemPlayer.SetSolo(stemName, isSolo);
    public void Dispose() => _stemPlayer.Dispose();
}
