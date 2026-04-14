using AiMusicWorkstation.Domain.Services;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Infrastructure.ExternalServices;

/// <summary>
/// Wrapper for IMetronome. The concrete implementation (Metronome)
/// is injected at runtime from Desktop layer.
/// </summary>
public class MetronomeService : IMetronome
{
    private readonly IMetronome _inner;
    private readonly ILogger<MetronomeService> _logger;
    
    public MetronomeService(IMetronome metronome, ILogger<MetronomeService> logger)
    {
        _inner = metronome ?? throw new ArgumentNullException(nameof(metronome));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task StartAsync(double bpm, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting metronome at {Bpm} BPM", bpm);
            await _inner.StartAsync(bpm, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting metronome");
            throw;
        }
    }
    
    public void Stop()
    {
        _logger.LogInformation("Stopping metronome");
        _inner.Stop();
    }
    
    public void Dispose()
    {
        _logger.LogInformation("Disposing MetronomeService");
        _inner.Dispose();
    }
}
