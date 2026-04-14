using AiMusicWorkstation.Domain.Services;

namespace AiMusicWorkstation.Desktop.Services;

/// <summary>
/// Adapter to make Metronome compatible with IMetronome
/// </summary>
public class MetronomeAdapter : IMetronome
{
    private readonly Metronome _metronome;
    
    public MetronomeAdapter(Metronome metronome)
    {
        _metronome = metronome ?? throw new ArgumentNullException(nameof(metronome));
    }
    
    public async Task StartAsync(double bpm, CancellationToken cancellationToken = default)
    {
        await _metronome.StartAsync(bpm);
    }
    
    public void Stop() => _metronome.Stop();
    
    public void Dispose() => _metronome.Dispose();
}
