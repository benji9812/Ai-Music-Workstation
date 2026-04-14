using AiMusicWorkstation.Domain.Services;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Infrastructure.ExternalServices;

/// <summary>
/// Wrapper for IAudioPlayer. The concrete implementation (StemPlayer) 
/// is injected at runtime from Desktop layer.
/// </summary>
public class AudioPlayerService : IAudioPlayer
{
    private readonly IAudioPlayer _inner;
    private readonly ILogger<AudioPlayerService> _logger;

    public TimeSpan CurrentTime
    {
        get => _inner.CurrentTime;
        set => _inner.CurrentTime = value;
    }

    public TimeSpan TotalTime => _inner.TotalTime;
    public bool IsPlaying => _inner.IsPlaying;
    public int SemitoneShift
    {
        get => _inner.SemitoneShift;
        set => _inner.SemitoneShift = value;
    }

    public AudioPlayerService(IAudioPlayer audioPlayer, ILogger<AudioPlayerService> logger) 
    {
        _inner = audioPlayer ?? throw new ArgumentNullException(nameof(audioPlayer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void LoadStems(string stemsPath)
    {
        _logger.LogInformation("Loading stems from {Path}", stemsPath);
        _inner.LoadStems(stemsPath);
    }

    public void Play()
    {
        _logger.LogInformation("Playing");
        _inner.Play();
    }

    public void Pause()
    {
        _logger.LogInformation("Pausing");
        _inner.Pause();
    }

    public void Stop()
    {
        _logger.LogInformation("Stopping");
        _inner.Stop();
    }

    public void SetMasterVolume(float volume)
    {
        _logger.LogDebug("Setting master volume to {Volume}", volume);
        _inner.SetMasterVolume(volume);
    }

    public void SetVolume(string stemName, float volume)
    {
        _logger.LogDebug("Setting volume for {Stem} to {Volume}", stemName, volume);
        _inner.SetVolume(stemName, volume);
    }

    public void SetMute(string stemName, bool isMuted)
    {
        _logger.LogDebug("Setting mute for {Stem} to {IsMuted}", stemName, isMuted);
        _inner.SetMute(stemName, isMuted);
    }

    public void SetSolo(string stemName, bool isSolo)
    {
        _logger.LogDebug("Setting solo for {Stem} to {IsSolo}", stemName, isSolo);
        _inner.SetSolo(stemName, isSolo);
    }

    public void Dispose()
    {
        _logger.LogInformation("Disposing AudioPlayerService");
        _inner.Dispose();
    }
}

