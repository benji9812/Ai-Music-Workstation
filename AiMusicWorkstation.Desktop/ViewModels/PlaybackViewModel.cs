using System.Windows.Input;
using AiMusicWorkstation.Application.Bus;
using AiMusicWorkstation.Application.Commands.Playback;
using AiMusicWorkstation.Application.Queries.Playback;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;

namespace AiMusicWorkstation.Desktop.ViewModels;

public class PlaybackViewModel : ViewModelBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly ILogger<PlaybackViewModel> _logger;
    
    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetProperty(ref _isPlaying, value);
    }
    
    private string _currentChord = "--";
    public string CurrentChord
    {
        get => _currentChord;
        set => SetProperty(ref _currentChord, value);
    }
    
    private TimeSpan _currentTime;
    public TimeSpan CurrentTime
    {
        get => _currentTime;
        set => SetProperty(ref _currentTime, value);
    }

    public ICommand PlayCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand StopCommand { get; }
    
    public PlaybackViewModel(
        ICommandBus commandBus,
        IQueryBus queryBus,
        ILogger<PlaybackViewModel> logger)
    {
        _commandBus = commandBus ?? throw new ArgumentNullException(nameof(commandBus));
        _queryBus = queryBus ?? throw new ArgumentNullException(nameof(queryBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        PlayCommand = new AsyncRelayCommand(ExecutePlayAsync);
        PauseCommand = new AsyncRelayCommand(ExecutePauseAsync);
        StopCommand = new AsyncRelayCommand(ExecuteStopAsync);
    }

    private async Task ExecutePlayAsync()
    {
        try
        {
            await _commandBus.Execute(new PlayCommand { StartMetronome = false });
            IsPlaying = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing play command");
        }
    }

    private async Task ExecutePauseAsync()
    {
        try
        {
            await _commandBus.Execute(new PauseCommand());
            IsPlaying = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing pause command");
        }
    }

    private async Task ExecuteStopAsync()
    {
        try
        {
            await _commandBus.Execute(new StopCommand());
            IsPlaying = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing stop command");
        }
    }
}
