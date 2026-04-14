namespace AiMusicWorkstation.Domain.Services;

public interface IAudioPlayer : IDisposable
{
    void LoadStems(string stemsPath);
    void Play();
    void Pause();
    void Stop();
    TimeSpan CurrentTime { get; set; }
    TimeSpan TotalTime { get; }
    bool IsPlaying { get; }
    void SetMasterVolume(float volume);
    void SetVolume(string stemName, float volume);
    void SetMute(string stemName, bool isMuted);
    void SetSolo(string stemName, bool isSolo);
    int SemitoneShift { get; set; }
}
