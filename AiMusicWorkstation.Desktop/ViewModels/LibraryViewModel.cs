using System.Collections.ObjectModel;
using System.Windows.Input;
using AiMusicWorkstation.Application.Bus;
using AiMusicWorkstation.Application.Commands.Library;
using AiMusicWorkstation.Application.Queries.Library;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using System.Runtime.CompilerServices;
using System.ComponentModel;

namespace AiMusicWorkstation.Desktop.ViewModels;

public class LibraryViewModel : ViewModelBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly ILogger<LibraryViewModel> _logger;
    
    private ObservableCollection<SongProjectDto> _projects = new();
    public ObservableCollection<SongProjectDto> Projects
    {
        get => _projects;
        set => SetProperty(ref _projects, value);
    }
    
    private string _searchTerm = string.Empty;
    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (SetPropertyIfChanged(ref _searchTerm, value))
            {
                _ = ApplyFilterAsync();
            }
        }
    }
    
    public ICommand DeleteCommand { get; }
    public ICommand RefreshCommand { get; }
    
    public LibraryViewModel(
        ICommandBus commandBus,
        IQueryBus queryBus,
        ILogger<LibraryViewModel> logger)
    {
        _commandBus = commandBus ?? throw new ArgumentNullException(nameof(commandBus));
        _queryBus = queryBus ?? throw new ArgumentNullException(nameof(queryBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        DeleteCommand = new AsyncRelayCommand<string>(ExecuteDelete);
        RefreshCommand = new AsyncRelayCommand(ExecuteRefresh);
        
        _ = LoadProjectsAsync();
    }
    
    public async Task LoadProjectsAsync()
    {
        try
        {
            await ApplyFilterAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading projects");
        }
    }
    
    private async Task ApplyFilterAsync()
    {
        try
        {
            var query = new FilterProjectsQuery
            {
                SearchTerm = _searchTerm
            };
            
            var results = await _queryBus.Execute(query);
            Projects = new ObservableCollection<SongProjectDto>(results);
            _logger.LogInformation("Loaded {Count} projects", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering projects");
        }
    }
    
    private async Task ExecuteDelete(string? projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId)) return;
        
        try
        {
            await _commandBus.Execute(new DeleteProjectCommand { ProjectId = projectId });
            await LoadProjectsAsync();
            _logger.LogInformation("Project deleted: {ProjectId}", projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project");
        }
    }
    
    private async Task ExecuteRefresh(object? parameter)
    {
        try
        {
            await LoadProjectsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing projects");
        }
    }
    
    private bool SetPropertyIfChanged<T>(ref T field, T newValue, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, newValue))
            return false;
        
        field = newValue;
        OnPropertyChanged(propertyName);
        return true;
    }
}
