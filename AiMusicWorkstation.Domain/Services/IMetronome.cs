namespace AiMusicWorkstation.Domain.Services;

public interface IMetronome : IDisposable
{
    Task StartAsync(double bpm, CancellationToken cancellationToken = default);
    void Stop();
}
