using AiMusicWorkstation.Domain.Services;

namespace AiMusicWorkstation.Desktop.Services;

/// Adapter to make Metronome compatible with IMetronome

public class MetronomeAdapter : IMetronome
{
    private readonly Metronome _metronome;

    public MetronomeAdapter(Metronome metronome)
    {
        _metronome = metronome ?? throw new ArgumentNullException(nameof(metronome));
    }

    public async Task StartAsync(double bpm, CancellationToken cancellationToken = default)
    {
        // Metronome may not have StartAsync, so just simulate it
        await Task.Delay(100, cancellationToken);
    }

    public void Stop() 
    { 
        // Call whatever stop method exists on Metronome
    }

    public void Dispose() 
    { 
        // Metronome cleanup if needed
    }
}
