# Modernization Plan: AI Music Workstation (CQRS & Clean Code, All-At-Once)

## Table of Contents
- Executive Summary
- Migration Strategy (Selected: All-At-Once)
- Implementation Timeline (Phases)
- Detailed Execution Steps
  - Prerequisites
  - Atomic Migration (TASK-001)
  - Test Validation (TASK-002)
- Dependency Analysis
- Project-by-Project Plans
- Package Update Reference
- Breaking Changes Catalog
- Testing & Validation Strategy
- Risk Management & Mitigation
- Source Control Strategy
- Success Criteria
- Appendices (Artifacts & References)

---

## Executive Summary

Selected Strategy
**All-At-Once Strategy** — All projects and layers are migrated simultaneously in one coordinated, atomic pass.

Rationale
- Solution is small-to-medium (3 existing projects) and homogeneous
- Assessment shows dependencies are modern and compatible with .NET 10
- Key modernization goals are architectural (CQRS, DI, MVVM, resource cleanup), not framework version upgrade
- All structural changes (new Application/Domain/Infrastructure projects, viewmodel extraction, commands/queries/handlers, DI) should be applied in a single coordinated operation to maintain a consistent codebase and avoid partial states

Scope
- Create/modify projects: `AiMusicWorkstation.Application` (NEW), `AiMusicWorkstation.Domain` (NEW), `AiMusicWorkstation.Infrastructure` (NEW), `AiMusicWorkstation.Tests` (NEW)
- Refactor `AiMusicWorkstation.Desktop` to move business logic into Application/Infrastructure layers and replace code-behind with ViewModels
- Migrate service implementations from Desktop/Services into Infrastructure/ExternalServices and introduce interfaces
- Implement CQRS primitives: Commands, Queries, Handlers, CommandBus, QueryBus
- Implement DI container registration and wiring in `App.xaml.cs`
- Add unit tests for handlers and domain value objects

Deliverable (end of atomic migration):
- Solution compiles with new projects included
- All new types (commands/queries/handlers/interfaces) present
- MainWindow reduced to view-only + ViewModel bindings
- Repository, Infrastructure services and DI registered

---

## Migration Strategy

All-At-Once specifics (how this plan applies):
- All code moves and API reference changes are applied in one atomic operation (TASK-001). This includes creation of new projects, new interfaces, moving service implementations, and updating all call sites to use new interfaces/DI.
- Prerequisites (SDK, global config) handled first (TASK-000) if applicable.
- Testing and fixes are executed after the atomic migration as separate validation task (TASK-002).
- Rationale: The changes are highly interdependent (e.g., moving PythonBridge and StemPlayer into Infrastructure then changing all consumers to injected interfaces). Applying simultaneously avoids long-lived intermediate states where types are partially moved.

Key principles for this All-At-Once pass:
- Use feature branch `feature/cqrs-refactor` (or similar) for atomic commit
- Keep code compilable at end of TASK-001
- Prefer single comprehensive commit for the migration content (or logically grouped commits but merged as a single PR)
- Run build and test suite immediately after TASK-001 to locate and fix compilation/regression issues

---

## Implementation Timeline (Phases)

Phase 0: Preparation
- Verify local environment (SDK, editor)
- Create feature branch for the atomic migration
- Ensure no pending uncommitted changes (commit/stash as appropriate)

Phase 1: Atomic Migration (TASK-001) — single coordinated pass
- Create new projects: Application, Domain, Infrastructure, Tests
- Define interfaces for services (IAudioPlayer, IPythonAnalysisService, ILibraryRepository, ISmartImporter, IMetronome)
- Move/implement services into Infrastructure, add adapters/wrappers where needed
- Implement Commands/Queries and Handlers
- Refactor MainWindow to ViewModels and replace direct service usage with injected interfaces
- Register all services, handlers and ViewModels in DI container (App.xaml.cs)
- Update usings, namespaces and project references
- Ensure solution builds

Phase 2: Test Validation (TASK-002)
- Run unit tests for Application and Domain handlers
- Run integration tests (if available)
- Execute defined validation checklist (build, tests, smoke checks)
- Address remaining compilation/runtime issues discovered

---

## Detailed Execution Steps

### TASK-000: Prerequisites
- Ensure development SDK and required tooling are installed (no framework change planned; remain on .NET 10)
- Verify repository state and create feature branch `feature/cqrs-refactor` from current working branch
- Commit or stash local pending changes per team policy

Deliverable: branch created and workspace ready for atomic migration

### TASK-001: Atomic CQRS & Clean Code Migration (All projects simultaneously)
**Scope**: Apply all code moves, new project creation, DI registration, and refactors in a single coordinated pass.  
**Principle**: All changes are interdependent and applied together to avoid intermediate states.

#### 1. Create New Projects & Project References

**Step 1.1: Create project files**
```powershell
# Create folders and .csproj files
mkdir AiMusicWorkstation.Application
mkdir AiMusicWorkstation.Domain
mkdir AiMusicWorkstation.Infrastructure
mkdir AiMusicWorkstation.Tests

# Each .csproj follows SDK style with TargetFramework net10.0
# Example AiMusicWorkstation.Application.csproj:
# <Project Sdk="Microsoft.NET.Sdk">
#   <PropertyGroup>
#     <TargetFramework>net10.0</TargetFramework>
#     <LangVersion>14.0</LangVersion>
#     <Nullable>enable</Nullable>
#     <ImplicitUsings>enable</ImplicitUsings>
#   </PropertyGroup>
#   <ItemGroup>
#     <ProjectReference Include="..\AiMusicWorkstation.Domain\AiMusicWorkstation.Domain.csproj" />
#     <ProjectReference Include="..\AiMusicWorkstation.Shared\AiMusicWorkstation.Shared.csproj" />
#   </ItemGroup>
#   <ItemGroup>
#     <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.3" />
#   </ItemGroup>
# </Project>
```

**Step 1.2: Add project references to solution**
- `AiMusicWorkstation.Desktop.csproj` → references Application, Shared, Infrastructure
- `AiMusicWorkstation.Application.csproj` → references Domain, Infrastructure, Shared
- `AiMusicWorkstation.Domain.csproj` → references Shared
- `AiMusicWorkstation.Infrastructure.csproj` → references Domain, Shared, External packages (NAudio, SpotifyAPI.Web, YoutubeExplode)
- `AiMusicWorkstation.Tests.csproj` → references Application, Domain, Infrastructure, xUnit, Moq

**Validation**: Solution builds with new projects (expect compilation errors until later steps fill in types)

---

#### 2. Define Service Interfaces in Domain Layer

**Step 2.1: Create `AiMusicWorkstation.Domain/Services/IAudioPlayer.cs`**
```csharp
namespace AiMusicWorkstation.Domain.Services
{
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
        void ExportMix(string outputPath, float drumVolume, float bassVolume, float otherVolume, float vocalVolume);
    }
}
```

**Step 2.2: Create `AiMusicWorkstation.Domain/Services/IPythonAnalysisService.cs`**
```csharp
namespace AiMusicWorkstation.Domain.Services
{
    public interface IPythonAnalysisService : IDisposable
    {
        Task<AnalysisResult> RunAnalysisAsync(string filePath, bool useCloud = true, CancellationToken cancellationToken = default);
        Task<AnalysisResult> ReAnalyzeAsync(string filePath, CancellationToken cancellationToken = default);
    }

    public class AnalysisResult
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public double Bpm { get; set; }
        public int TimeSignature { get; set; }
        public string Key { get; set; }
        public string StemsPath { get; set; }
        public List<LyricSegment> Lyrics { get; set; }
        public List<ChordEvent> Chords { get; set; }
    }
}
```

**Step 2.3: Create `AiMusicWorkstation.Domain/Services/ILibraryRepository.cs`**
```csharp
namespace AiMusicWorkstation.Domain.Repositories
{
    public interface ILibraryRepository
    {
        Task<List<SongProject>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<SongProject> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task AddAsync(SongProject project, CancellationToken cancellationToken = default);
        Task UpdateAsync(SongProject project, CancellationToken cancellationToken = default);
        Task DeleteAsync(string id, CancellationToken cancellationToken = default);
        Task SaveAsync(CancellationToken cancellationToken = default);
    }
}
```

**Step 2.4: Create `AiMusicWorkstation.Domain/Services/ISmartImporterService.cs`**
```csharp
namespace AiMusicWorkstation.Domain.Services
{
    public interface ISmartImporterService
    {
        Task<DownloadResult> DownloadSongAsync(string url, IProgress<string> progress, CancellationToken cancellationToken = default);
        Task<Metadata> GetOfficialMetadata(string trackId);
        Task<string> GetGenreFromMusicBrainz(string artistName, string songTitle);
        string ExtractSpotifyId(string spotifyUrl);
    }

    public class DownloadResult
    {
        public string FilePath { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
    }

    public class Metadata
    {
        public string Genre { get; set; }
    }
}
```

**Step 2.5: Create `AiMusicWorkstation.Domain/Services/IMetronome.cs`**
```csharp
namespace AiMusicWorkstation.Domain.Services
{
    public interface IMetronome : IDisposable
    {
        int TimeSignature { get; set; }
        bool IsPlaying { get; }
        void Start(double bpm, bool withCountIn = false);
        Task StartAsync(double bpm, bool withCountIn = false, CancellationToken cancellationToken = default);
        void Stop();
        void ResetEvents();
        event Action<int> OnBeat;
        event Action OnCountInComplete;
    }
}
```

**Validation**: Domain project compiles; interfaces are clean contracts without implementation details

---

#### 3. Move Implementations to Infrastructure Layer

**Step 3.1: Create `AiMusicWorkstation.Infrastructure/ExternalServices/PythonBridgeService.cs`**
```csharp
namespace AiMusicWorkstation.Infrastructure.ExternalServices
{
    public class PythonBridgeService : IPythonAnalysisService
    {
        private readonly ILogger<PythonBridgeService> _logger;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
        private static readonly HttpClient _httpClient = new();
        private Process _pythonProcess;
        private bool _disposed = false;

        public PythonBridgeService(ILogger<PythonBridgeService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _retryPolicy = Policy
                .Handle<HttpRequestException>()
                .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    onRetry: (outcome, delay, attemptNumber, context) =>
                    {
                        _logger.LogWarning($"Retry {attemptNumber} after {delay.TotalSeconds}s");
                    });
        }

        public async Task<AnalysisResult> RunAnalysisAsync(string filePath, bool useCloud = true, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    throw new ArgumentNullException(nameof(filePath));

                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"File not found: {filePath}");

                // Ensure Python server running
                await EnsureServerIsRunning(cancellationToken);

                // Stream file instead of loading to memory
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var content = new MultipartFormDataContent();
                content.Add(new StreamContent(fileStream), "file", Path.GetFileName(filePath));
                content.Add(new StringContent(useCloud.ToString()), "use_cloud");

                // Execute with retry policy
                var response = await _retryPolicy.ExecuteAsync(async ct =>
                    await _httpClient.PostAsync("http://127.0.0.1:8000/analyze", content, ct),
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError($"Analysis failed: {errorContent}");
                    throw new Exception($"Analysis failed: {response.StatusCode}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                var analysisResult = JsonSerializer.Deserialize<AnalysisResult>(jsonResponse);
                return analysisResult ?? throw new Exception("Failed to deserialize analysis result");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RunAnalysisAsync");
                throw;
            }
        }

        private async Task EnsureServerIsRunning(CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.GetAsync("http://127.0.0.1:8000/health", cancellationToken);
                if (response.IsSuccessStatusCode) return;
            }
            catch { /* Server not running */ }

            // Start Python server if not running
            StartPythonServer();
            await Task.Delay(2000, cancellationToken); // Wait for server startup
        }

        private void StartPythonServer()
        {
            if (_pythonProcess != null && !_pythonProcess.HasExited) return;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "path/to/server.py",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _pythonProcess = Process.Start(startInfo);
                _pythonProcess.EnableRaisingEvents = true;
                _pythonProcess.Exited += (s, e) =>
                {
                    _logger.LogWarning("Python server crashed");
                };
                _logger.LogInformation("Python server started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Python server");
                throw;
            }
        }

        public async Task<AnalysisResult> ReAnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return await RunAnalysisAsync(filePath, useCloud: false, cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _pythonProcess?.Kill();
                _pythonProcess?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing PythonBridgeService");
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~PythonBridgeService()
        {
            Dispose();
        }
    }
}
```

**Step 3.2: Create `AiMusicWorkstation.Infrastructure/ExternalServices/AudioPlayerService.cs`** (wrapper)
```csharp
namespace AiMusicWorkstation.Infrastructure.ExternalServices
{
    public class AudioPlayerService : IAudioPlayer
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

        public AudioPlayerService(StemPlayer stemPlayer)
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
        public void ExportMix(string outputPath, float drumVolume, float bassVolume, float otherVolume, float vocalVolume)
            => _stemPlayer.ExportMix(outputPath, drumVolume, bassVolume, otherVolume, vocalVolume);

        public void Dispose()
        {
            _stemPlayer?.Dispose();
        }
    }
}
```

**Step 3.3: Create `AiMusicWorkstation.Infrastructure/Persistence/LibraryRepository.cs`**
```csharp
namespace AiMusicWorkstation.Infrastructure.Persistence
{
    public class LibraryRepository : ILibraryRepository
    {
        private readonly string _libraryFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AiMusicWorkstation", "library.json");
        private List<SongProject> _projects;
        private readonly ILogger<LibraryRepository> _logger;

        public LibraryRepository(ILogger<LibraryRepository> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            LoadLibraryInternal();
        }

        public async Task<List<SongProject>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(_projects.ToList());
        }

        public async Task<SongProject> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(_projects.FirstOrDefault(p => p.Id == id));
        }

        public async Task AddAsync(SongProject project, CancellationToken cancellationToken = default)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (string.IsNullOrWhiteSpace(project.Id)) project.Id = Guid.NewGuid().ToString();
            _projects.Add(project);
            await Task.CompletedTask;
        }

        public async Task UpdateAsync(SongProject project, CancellationToken cancellationToken = default)
        {
            var existing = _projects.FirstOrDefault(p => p.Id == project.Id);
            if (existing != null)
            {
                var index = _projects.IndexOf(existing);
                _projects[index] = project;
            }
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            _projects.RemoveAll(p => p.Id == id);
            await Task.CompletedTask;
        }

        public async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var directory = Path.GetDirectoryName(_libraryFile);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_projects, options);
                await File.WriteAllTextAsync(_libraryFile, json, cancellationToken);
                _logger.LogInformation("Library saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving library");
                throw;
            }
        }

        private void LoadLibraryInternal()
        {
            try
            {
                if (File.Exists(_libraryFile))
                {
                    var json = File.ReadAllText(_libraryFile);
                    _projects = JsonSerializer.Deserialize<List<SongProject>>(json) ?? new List<SongProject>();
                }
                else
                {
                    _projects = new List<SongProject>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading library; starting with empty library");
                _projects = new List<SongProject>();
            }
        }
    }
}
```

**Validation**: Infrastructure project compiles; services implement interfaces cleanly

---

#### 4. Create Application Layer (CQRS)

**Step 4.1: Create command classes** (example: PlayCommand)
```csharp
// AiMusicWorkstation.Application/Commands/Playback/PlayCommand.cs
namespace AiMusicWorkstation.Application.Commands.Playback
{
    public class PlayCommand : ICommand
    {
        public bool StartMetronome { get; set; }
    }
}
```

**Step 4.2: Create command handlers** (example: PlayCommandHandler)
```csharp
// AiMusicWorkstation.Application/Handlers/Commands/PlayCommandHandler.cs
namespace AiMusicWorkstation.Application.Handlers.Commands
{
    public class PlayCommandHandler : ICommandHandler<PlayCommand>
    {
        private readonly IAudioPlayer _player;
        private readonly IMetronome _metronome;
        private readonly ILogger<PlayCommandHandler> _logger;

        public PlayCommandHandler(IAudioPlayer player, IMetronome metronome, ILogger<PlayCommandHandler> logger)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _metronome = metronome ?? throw new ArgumentNullException(nameof(metronome));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Handle(PlayCommand command, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_player.IsPlaying) return;

                _player.Play();
                _logger.LogInformation("Playback started");

                if (command.StartMetronome)
                {
                    await _metronome.StartAsync(120); // Default BPM; will be overridden in ViewModel
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting playback");
                throw;
            }
        }
    }
}
```

**Step 4.3: Create query classes and handlers** (similarly)

**Step 4.4: Create CommandBus and QueryBus**
```csharp
// AiMusicWorkstation.Application/Bus/CommandBus.cs
namespace AiMusicWorkstation.Application.Bus
{
    public interface ICommandBus
    {
        Task Execute<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : class;
    }

    public class CommandBus : ICommandBus
    {
        private readonly IServiceProvider _serviceProvider;

        public CommandBus(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task Execute<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : class
        {
            var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
            dynamic handler = _serviceProvider.GetService(handlerType) ?? throw new InvalidOperationException($"No handler for {command.GetType().Name}");
            await handler.Handle((dynamic)command, cancellationToken);
        }
    }
}
```

**Validation**: All command/query/handler files created; Application layer compiles

---

#### 5. Refactor Desktop UI to ViewModels

**Step 5.1: Create `AiMusicWorkstation.Desktop/ViewModels/ViewModelBase.cs`**
```csharp
namespace AiMusicWorkstation.Desktop.ViewModels
{
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void SetProperty<T>(ref T backingField, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(backingField, value)) return;
            backingField = value;
            OnPropertyChanged(propertyName);
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
```

**Step 5.2: Create `AiMusicWorkstation.Desktop/ViewModels/PlaybackViewModel.cs`**
```csharp
namespace AiMusicWorkstation.Desktop.ViewModels
{
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

        public ICommand PlayCommand { get; private set; }
        public ICommand PauseCommand { get; private set; }
        public ICommand StopCommand { get; private set; }

        public PlaybackViewModel(ICommandBus commandBus, IQueryBus queryBus, ILogger<PlaybackViewModel> logger)
        {
            _commandBus = commandBus ?? throw new ArgumentNullException(nameof(commandBus));
            _queryBus = queryBus ?? throw new ArgumentNullException(nameof(queryBus));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            PlayCommand = new AsyncRelayCommand(ExecutePlayCommand);
            PauseCommand = new AsyncRelayCommand(ExecutePauseCommand);
            StopCommand = new AsyncRelayCommand(ExecuteStopCommand);
        }

        private async Task ExecutePlayCommand(object parameter)
        {
            try
            {
                await _commandBus.Execute(new PlayCommand { StartMetronome = false });
                IsPlaying = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error playing");
            }
        }

        private async Task ExecutePauseCommand(object parameter)
        {
            try
            {
                await _commandBus.Execute(new PauseCommand());
                IsPlaying = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing");
            }
        }

        private async Task ExecuteStopCommand(object parameter)
        {
            try
            {
                await _commandBus.Execute(new StopCommand());
                IsPlaying = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping");
            }
        }
    }
}
```

**Step 5.3: Update `MainWindow.xaml.cs`** (delete business logic, add ViewModel injection)
```csharp
namespace AiMusicWorkstation.Desktop
{
    public partial class MainWindow : Window
    {
        private PlaybackViewModel _playbackViewModel;

        public MainWindow(PlaybackViewModel playbackViewModel)
        {
            InitializeComponent();
            _playbackViewModel = playbackViewModel ?? throw new ArgumentNullException(nameof(playbackViewModel));
            DataContext = _playbackViewModel;

            Closing += (s, e) => { /* cleanup if needed */ };
        }
    }
}
```

**Step 5.4: Update `MainWindow.xaml` to use command bindings**
```xml
<!-- BEFORE -->
<Button x:Name="PlayPauseBtn" Click="PlayPause_Click" Content="▶" />

<!-- AFTER -->
<Button Command="{Binding PlayCommand}" Content="▶" />
```

**Validation**: MainWindow builds with ViewModels; no compilation errors in code-behind

---

#### 6. Implement Dependency Injection in App.xaml.cs

**Step 6.1: Update `App.xaml.cs` with full DI configuration**
```csharp
namespace AiMusicWorkstation
{
    public partial class App : Application
    {
        private IServiceProvider _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            var services = new ServiceCollection();

            // Logging
            services.AddLogging(config =>
            {
                config.AddConsole();
                config.AddDebug();
            });

            // Configuration
            var config = new ConfigurationBuilder()
                .AddUserSecrets<App>()
                .Build();
            services.AddSingleton<IConfiguration>(config);

            // Domain & Infrastructure services
            services.AddSingleton<StemPlayer>();
            services.AddSingleton<IAudioPlayer>(sp => new AudioPlayerService(sp.GetRequiredService<StemPlayer>()));
            services.AddSingleton<IPythonAnalysisService, PythonBridgeService>();
            services.AddSingleton<ISmartImporterService, SmartImporterService>();
            services.AddSingleton<IMetronome, Metronome>();
            services.AddSingleton<ILibraryRepository, LibraryRepository>();

            // CQRS
            services.AddSingleton<ICommandBus, CommandBus>();
            services.AddSingleton<IQueryBus, QueryBus>();

            // Command Handlers (register all 15)
            services.AddScoped<ICommandHandler<PlayCommand>, PlayCommandHandler>();
            services.AddScoped<ICommandHandler<PauseCommand>, PauseCommandHandler>();
            services.AddScoped<ICommandHandler<StopCommand>, StopCommandHandler>();
            // ... (12 more command handlers)

            // Query Handlers (register all 13)
            services.AddScoped<IQueryHandler<GetPlaybackStateQuery, PlaybackStateDto>, GetPlaybackStateQueryHandler>();
            services.AddScoped<IQueryHandler<GetCurrentChordQuery, ChordDto>, GetCurrentChordQueryHandler>();
            // ... (11 more query handlers)

            // ViewModels
            services.AddSingleton<PlaybackViewModel>();
            services.AddSingleton<LibraryViewModel>();
            services.AddSingleton<AnalysisViewModel>();
            services.AddSingleton<MainWindowViewModel>();

            // Views
            services.AddSingleton<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }
    }
}
```

**Validation**: App.xaml.cs compiles; all services registered

---

#### 7. Build & Verification

**Step 7.1: Full solution build**
- Build entire solution
- Resolve any compilation errors (mostly namespace updates, missing usings)
- Verify all projects compile successfully

**Step 7.2: Reference updates checklist**
- [ ] All usings updated to new namespaces
- [ ] All command/query references in ViewModels point to correct classes
- [ ] All handler registrations in DI match actual handler classes
- [ ] No circular dependencies between projects
- [ ] All external package references included

**Deliverable**: 
- Solution compiles with zero errors
- All new projects created and wired
- MainWindow reduced to view + ViewModel bindings
- Services moved to Infrastructure with interfaces in Domain
- All commands/queries and handlers present in Application layer
- DI container fully configured

### TASK-002: Test Validation and Fixes
Scope: Validate the atomic migration and fix issues revealed by build and tests

Operations:
- Run full solution build (verify no compile errors)
- Run unit tests in `AiMusicWorkstation.Tests` and address failures
- Verify core runtime behaviors (smoke checks): playback, import, export, chord rendering, lyrics scrolling
- Address runtime exceptions and resource leaks
- Harden IPythonAnalysisService error handling and retry policies (use Polly where appropriate)

Deliverable: All tests pass; smoke checks satisfied; code ready for PR

---

## Detailed Dependency Analysis

### Project Dependency Graph (Pre-Migration)
```
AiMusicWorkstation.Desktop (DESKTOP - 1,100 LOC MainWindow)
├─ Directly instantiates:
│  ├─ PythonBridge (manages Python server & analysis)
│  ├─ StemPlayer (audio playback with pitch shift)
│  ├─ LibraryManager (library persistence)
│  ├─ SmartImporter (YouTube/Spotify download)
│  └─ Metronome (click generation)
├─ References: AiMusicWorkstation.Shared
└─ External deps: NAudio, SpotifyAPI.Web, YoutubeExplode, Microsoft.Extensions.Configuration

AiMusicWorkstation.Shared (SHARED - utilities)
├─ MusicTheoryHelper (key/chord transposition)
└─ Models (SongProject, ChordEvent, LyricSegment, etc.)
```

### Project Dependency Graph (Post-Migration - All-At-Once)
```
AiMusicWorkstation.Desktop (VIEW + VIEWMODELS)
├─ Injects: ICommandBus, IQueryBus, ViewModels (from DI)
├─ References: 
│  ├─ AiMusicWorkstation.Application (commands, queries, handlers)
│  └─ AiMusicWorkstation.Shared (DTOs)
└─ No direct service instantiation

AiMusicWorkstation.Application (COMMANDS/QUERIES/HANDLERS)
├─ Commands/ (15 command classes + DTOs)
├─ Queries/ (13 query classes + DTOs)
├─ Handlers/ (28 command handlers + 13 query handlers)
├─ Bus/ (CommandBus, QueryBus implementations)
├─ References:
│  ├─ AiMusicWorkstation.Domain (interfaces)
│  ├─ AiMusicWorkstation.Infrastructure (injected services)
│  └─ AiMusicWorkstation.Shared (DTOs)
└─ Microsoft.Extensions.Logging

AiMusicWorkstation.Domain (INTERFACES + ENTITIES + VALUE OBJECTS)
├─ Service interfaces (IAudioPlayer, IPythonAnalysisService, ILibraryRepository, etc.)
├─ Entities (SongProjectEntity, PlaybackStateEntity)
├─ ValueObjects (Key, Chord, Volume, TimeSpan)
├─ References:
│  └─ AiMusicWorkstation.Shared (common constants/enums)
└─ No external dependencies

AiMusicWorkstation.Infrastructure (IMPLEMENTATIONS + PERSISTENCE)
├─ ExternalServices/
│  ├─ PythonBridgeService.cs (implements IPythonAnalysisService)
│  ├─ AudioPlayerService.cs (implements IAudioPlayer, wraps StemPlayer)
│  ├─ SmartImporterService.cs (implements ISmartImporterService)
│  └─ MetronomeService.cs (implements IMetronome)
├─ Persistence/
│  └─ LibraryRepository.cs (implements ILibraryRepository)
├─ References:
│  ├─ AiMusicWorkstation.Domain (interfaces)
│  ├─ AiMusicWorkstation.Shared (models)
│  └─ External: NAudio, SpotifyAPI.Web, YoutubeExplode
└─ Logging & error handling

AiMusicWorkstation.Shared (SHARED - unchanged)
├─ DTOs and models
├─ Helpers (MusicTheoryHelper)
└─ No external dependencies

AiMusicWorkstation.Tests (UNIT TESTS - new)
├─ Application/ (command/query handler tests)
└─ Domain/ (value object & domain service tests)
```

### Critical Dependency Points & Solutions

| Dependency | FROM | TO | Migration Action |
|---|---|---|---|
| **PythonBridge usage** | MainWindow (20+ callsites) | IPythonAnalysisService via DI | Commands inject IPythonAnalysisService; handlers use it; DI registers PythonBridgeService |
| **StemPlayer usage** | MainWindow (15+ callsites) | IAudioPlayer via DI | Commands/Queries access _player via DI; handlers coordinate playback |
| **LibraryManager persistence** | MainWindow (10+ callsites) | ILibraryRepository via DI | Commands/Queries use ILibraryRepository; handlers save/load projects |
| **SmartImporter** | MainWindow (5 callsites) | ISmartImporterService via DI | ImportSongCommand handler uses injected service |
| **Metronome** | MainWindow (8 callsites) | IMetronome via DI | PlayCommand handler manages metronome via DI |
| **Project references** | Desktop → Services | Desktop → Application + Infrastructure | New projects define interfaces (Domain), implementations (Infrastructure), handlers (Application) |

### Migration Order (Atomic = all simultaneous)
All new types created together in single pass:
1. Domain interfaces (IAudioPlayer, IPythonAnalysisService, etc.)
2. Infrastructure implementations (concrete classes)
3. Application commands/queries/handlers
4. Desktop ViewModels replacing code-behind
5. DI wiring in App.xaml.cs
6. All usings, namespaces, references updated in one atomic commit

**Key principle**: No intermediate states where code partially references old types and new types.

---

## Project-by-Project Plans

### `AiMusicWorkstation.Desktop` (WPF UI)
**Current state**: View + 1,100 LOC code-behind with business logic and direct service instantiation  
**Target state**: Views + ViewModels. All business logic in Application layer. DI-injected interfaces only.

**Files to modify**:
- `App.xaml.cs` — Add DI container setup (ServiceCollection, service registration, main window resolution)
- `MainWindow.xaml.cs` — Delete business logic methods (~700 LOC); replace with ViewModel injection and command bindings
- `EditSectionWindow.xaml.cs` — Similar pattern
- `InputWindow.xaml.cs` — May require minimal changes

**New files to create**:
- `ViewModels/MainWindowViewModel.cs` (state properties + command definitions)
- `ViewModels/PlaybackViewModel.cs` (playback state + play/pause/stop commands)
- `ViewModels/LibraryViewModel.cs` (library projects + search/filter commands)
- `ViewModels/AnalysisViewModel.cs` (analysis result display)
- `ViewModels/ViewModelBase.cs` (INotifyPropertyChanged base class)

**Specific changes**:
- Replace all `new PythonBridge()`, `new StemPlayer()`, `new LibraryManager()`, `new SmartImporter()`, `new Metronome()` with DI via constructor
- Delete `Timer_Tick()` and replace with `GetCurrentChordQuery` via ViewModel binding
- Delete `PlayPause_Click()` and replace with `PlayCommand` binding
- Delete `RefreshLibrary()` and replace with `FilterProjectsQuery` binding
- Delete service initialization fields; declare ViewModels instead
- Update XAML button/slider bindings to Command="{Binding PlayCommand}" instead of Click event handlers

**Code before/after example**:
```csharp
// BEFORE
public partial class MainWindow : Window
{
    private PythonBridge _pythonBridge = new PythonBridge();

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        _player.Play();
        PlayPauseBtn.Content = "⏸";
        // ... more logic
    }
}

// AFTER
public partial class MainWindow : Window
{
    private PlaybackViewModel _viewModel;

    public MainWindow(PlaybackViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        // No manual click handlers; bindings to Commands
    }
}
```

**Validation**:
- MainWindow loads without errors
- ViewModel properties bind to UI controls
- Command execution triggers handler flow (verify via breakpoint)

---

### `AiMusicWorkstation.Shared` (Shared library)
**Current state**: Helpers and models scattered  
**Target state**: Clean DTO folder, helpers, and shared constants

**Files to keep**:
- `Helpers/MusicTheoryHelper.cs` (unchanged)
- `Models/SongProject.cs` (keep but may move to Shared.Dto after migration)

**New files to create**:
- `Dto/SongProjectDto.cs` (data transfer object for project)
- `Dto/AnalysisResultDto.cs` (analysis result transfer)
- `Dto/ChordDto.cs` (chord data transfer)
- `Dto/PlaybackStateDto.cs` (current playback state)
- `Enums/KeySource.cs` (enum for key source)

**Validation**:
- Shared project has no external dependencies
- DTOs map cleanly from Infrastructure to Application

---

### `AiMusicWorkstation.Api` (API project)
**Current state**: Minimal/scaffolding  
**Target state**: Optional; may be used to expose Application commands/queries as REST endpoints

**Action**: No mandatory migration. If REST API is needed in future, reference `AiMusicWorkstation.Application` and wrap commands/queries in `ApiController` methods.

---

### `AiMusicWorkstation.Application` (NEW - CQRS Layer)
**Purpose**: Commands, Queries, Handlers, and Buses

**Folder structure**:
```
AiMusicWorkstation.Application/
├── Commands/
│   ├── Playback/
│   │   ├── PlayCommand.cs
│   │   ├── PauseCommand.cs
│   │   ├── StopCommand.cs
│   │   ├── SetTransposeCommand.cs
│   │   └── SaveLyricsCommand.cs
│   ├── Library/
│   │   ├── ImportSongCommand.cs
│   │   ├── DeleteProjectCommand.cs
│   │   ├── RenameProjectCommand.cs
│   │   ├── MoveToGroupCommand.cs
│   │   ├── RefreshMetadataCommand.cs
│   │   └── DownloadSongCommand.cs
│   ├── Audio/
│   │   ├── SetVolumeCommand.cs
│   │   ├── SetMuteCommand.cs
│   │   └── SetSoloCommand.cs
│   └── ICommand.cs (base interface)
├── Queries/
│   ├── Playback/
│   │   ├── GetPlaybackStateQuery.cs
│   │   ├── GetCurrentChordQuery.cs
│   │   ├── GetUpcomingChordsQuery.cs
│   │   ├── GetActiveLyricsQuery.cs
│   │   ├── GetCurrentTimeQuery.cs
│   │   └── GetCurrentKeyQuery.cs
│   ├── Library/
│   │   ├── GetAllProjectsQuery.cs
│   │   ├── GetProjectByIdQuery.cs
│   │   ├── FilterProjectsQuery.cs
│   │   ├── SearchProjectsQuery.cs
│   │   ├── GetGenresQuery.cs
│   │   └── GetSortedProjectsQuery.cs
│   ├── Analysis/
│   │   └── GetAnalysisResultQuery.cs
│   └── IQuery.cs (base interface)
├── Handlers/
│   ├── Commands/
│   │   ├── PlayCommandHandler.cs
│   │   ├── ImportSongCommandHandler.cs
│   │   ├── ... (26 more handlers)
│   │   └── ICommandHandler.cs (base interface)
│   ├── Queries/
│   │   ├── GetCurrentChordQueryHandler.cs
│   │   ├── FilterProjectsQueryHandler.cs
│   │   ├── ... (11 more handlers)
│   │   └── IQueryHandler.cs (base interface)
├── Bus/
│   ├── ICommandBus.cs
│   ├── IQueryBus.cs
│   ├── CommandBus.cs
│   └── QueryBus.cs
└── Exceptions/
    ├── CommandExecutionException.cs
    ├── QueryExecutionException.cs
    └── ValidationException.cs
```

**Key implementation notes**:
- Command classes are simple DTOs with input parameters (e.g., `PlayCommand { StartMetronome: bool }`)
- Query classes are simple DTOs with output type parameters (e.g., `GetCurrentChordQuery : IQuery<ChordDto>`)
- Handlers encapsulate business logic (orchestrate infrastructure services)
- CommandBus and QueryBus are responsible for resolving and invoking handlers
- All handlers support async operations and cancellation tokens

**Validation**:
- All handler classes inherit from `ICommandHandler<T, TResponse>` or `IQueryHandler<T, TResponse>`
- CommandBus returns response objects with Success/Message fields
- Handlers are registered in DI container

---

### `AiMusicWorkstation.Domain` (NEW - Domain Layer)
**Purpose**: Domain entities, value objects, domain services, and interfaces

**Folder structure**:
```
AiMusicWorkstation.Domain/
├── Entities/
│   ├── SongProjectEntity.cs
│   ├── PlaybackStateEntity.cs
│   └── AnalysisEntity.cs
├── ValueObjects/
│   ├── Key.cs (immutable, represents musical key)
│   ├── Chord.cs (immutable, represents chord)
│   ├── Volume.cs (immutable, represents volume level 0-1)
│   └── TimeSpan.cs (immutable, represents duration)
├── Services/
│   ├── IAudioPlayer.cs (interface)
│   ├── IPythonAnalysisService.cs (interface)
│   ├── ILibraryRepository.cs (interface)
│   ├── ISmartImporterService.cs (interface)
│   ├── IMetronome.cs (interface)
│   └── IMusicTheoryDomainService.cs (interface)
├── Events/
│   ├── DomainEvent.cs (base event)
│   ├── SongPlayedEvent.cs
│   └── ProjectDeletedEvent.cs
└── Repositories/
    └── ILibraryRepository.cs (moved from Services folder)
```

**Key details**:
- Value objects are immutable and represent domain concepts
- Entity classes hold state and invariants
- Service interfaces define contracts for Infrastructure implementations
- Events represent domain events (optional for v1)

**Validation**:
- Domain project has no external dependencies (only Shared)
- Interfaces are well-defined and testable

---

### `AiMusicWorkstation.Infrastructure` (NEW - Infrastructure Layer)
**Purpose**: Concrete service implementations and persistence

**Folder structure**:
```
AiMusicWorkstation.Infrastructure/
├── ExternalServices/
│   ├── PythonBridgeService.cs (implements IPythonAnalysisService)
│   ├── AudioPlayerService.cs (implements IAudioPlayer, wraps StemPlayer)
│   ├── SmartImporterService.cs (implements ISmartImporterService, wraps SmartImporter)
│   └── MetronomeService.cs (implements IMetronome, wraps Metronome)
├── Persistence/
│   └── LibraryRepository.cs (implements ILibraryRepository, wraps LibraryManager JSON logic)
└── Logging/
    ├── ILogger.cs (alias to Microsoft.Extensions.Logging.ILogger)
    └── LoggerExtensions.cs (optional helper methods)
```

**Key implementation details**:

**PythonBridgeService.cs**:
- Implements `IPythonAnalysisService`
- Tracks `Process` reference for cleanup on `Dispose()`
- Implements exponential backoff retry (via Polly)
- Streams large audio files instead of loading to memory
- Proper error logging and user feedback

**AudioPlayerService.cs**:
- Wraps existing `StemPlayer` class
- Implements `IAudioPlayer` interface
- Delegates method calls to internal StemPlayer instance
- Ensures proper disposal of underlying resources

**LibraryRepository.cs**:
- Replaces `LibraryManager` persistence logic
- Implements `ILibraryRepository` interface
- Uses System.Text.Json for serialization
- Async save/load methods
- Robust error handling for file I/O

**Validation**:
- All services implement their respective interfaces
- Process cleanup tested (no orphaned Python processes)
- File I/O errors handled gracefully

---

### `AiMusicWorkstation.Tests` (NEW - Test Project)
**Purpose**: Unit tests for Application layer handlers and Domain layer value objects

**Folder structure**:
```
AiMusicWorkstation.Tests/
├── Application/
│   ├── Commands/
│   │   ├── PlayCommandHandlerTests.cs
│   │   ├── ImportSongCommandHandlerTests.cs
│   │   └── ... (key handler tests)
│   ├── Queries/
│   │   ├── GetCurrentChordQueryHandlerTests.cs
│   │   ├── FilterProjectsQueryHandlerTests.cs
│   │   └── ... (key query handler tests)
│   └── Bus/
│       ├── CommandBusTests.cs
│       └── QueryBusTests.cs
└── Domain/
    ├── ValueObjects/
    │   ├── KeyTests.cs
    │   ├── ChordTests.cs
    │   └── VolumeTests.cs
    └── Repositories/
        └── LibraryRepositoryTests.cs
```

**Test framework**: xUnit + Moq

**Key test patterns**:
- Handler tests mock dependencies (IAudioPlayer, IPythonAnalysisService, etc.)
- Query handler tests verify filtering/sorting logic
- Command handler tests verify state changes and side effects
- Value object tests verify immutability and domain logic

**Validation**:
- All handler tests pass
- Value object tests verify constraints
- Repository tests verify persistence contract

---

## Package Update Reference

Assessment found no mandatory NuGet upgrades for this modernization. Current package versions are compatible with .NET 10 and the planned refactor:
- `NAudio` 2.2.1
- `SpotifyAPI.Web` 7.3.0
- `YoutubeExplode` 6.5.7

If updates are desired, include them in the atomic pass; otherwise keep versions unchanged to reduce risk.

---

## Breaking Changes Catalog (Expected)

These are the likely compilation/runtime issues to expect and how to address them:

1. **Type/Namespace Moves**
   - Symptom: Missing type or namespace errors after moving classes
   - Mitigation: Add temporary adapter classes in old namespaces that forward calls to new interfaces; update usings across solution; run build and fix errors in a single pass

2. **Constructor DI Changes**
   - Symptom: Classes previously using `new` now require constructor parameters
   - Mitigation: Update consumers to obtain services from DI or CommandBus/QueryBus; add small factories where necessary

3. **Interface Contracts**
   - Symptom: Newly defined interfaces differ slightly from inlined implementations
   - Mitigation: Define interfaces carefully before moving implementations; add unit tests for interface conformance

4. **Asynchronous Behavior Changes**
   - Symptom: Some event handlers moved to async handlers may change timing
   - Mitigation: Preserve synchronization contexts where UI-related updates occur by using Dispatcher.Invoke when necessary inside handlers; validate metronome timing and playback start behavior

5. **Resource Lifetime & Disposal**
   - Symptom: Duplicate disposal or missing disposal leading to object disposed exceptions
   - Mitigation: Centralize disposal in infrastructure service and ensure DI lifetimes are correct (Singleton vs Scoped)

6. **JSON & Data Contract Changes**
   - Symptom: Serialization breakages when DTO shapes change
   - Mitigation: Maintain compatibility during migration; version DTOs if needed

---

## Testing & Validation Strategy

### TASK-002: Test Validation and Fixes (After atomic migration TASK-001 completes)

**Scope**: Validate the atomic migration and fix issues revealed by build, compilation, and tests

#### Phase 2.1: Compilation & Build Validation

**Step 2.1.1: Full solution build**
```bash
dotnet clean
dotnet build --configuration Release
```
**Expected outcome**: Zero compilation errors  
**If errors occur**:
- Review namespace mismatches (e.g., `System.IO` missing in Infrastructure)
- Verify project references are correctly specified
- Check for circular dependencies
- Update any remaining direct `new StemPlayer()` calls to injected interfaces

**Step 2.1.2: Verify all project references**
Checklist:
- [ ] Desktop → Application, Shared, Infrastructure (optional but preferred)
- [ ] Application → Domain, Infrastructure, Shared
- [ ] Domain → Shared only
- [ ] Infrastructure → Domain, Shared, NuGet packages
- [ ] Tests → Application, Domain, Infrastructure, xUnit, Moq

**Step 2.1.3: Check namespace consistency**
- [ ] All `AiMusicWorkstation.Application.*` types correctly namespaced
- [ ] All `AiMusicWorkstation.Domain.*` types correctly namespaced
- [ ] All `AiMusicWorkstation.Infrastructure.*` types correctly namespaced
- [ ] No naming conflicts across projects

---

#### Phase 2.2: Unit Test Execution

**Step 2.2.1: Create minimal test fixtures** (example)
```csharp
// AiMusicWorkstation.Tests/Application/Commands/PlayCommandHandlerTests.cs
public class PlayCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenPlayerNotPlaying_StartsPlayback()
    {
        // Arrange
        var mockPlayer = new Mock<IAudioPlayer>();
        var mockMetronome = new Mock<IMetronome>();
        var mockLogger = new Mock<ILogger<PlayCommandHandler>>();

        mockPlayer.Setup(p => p.IsPlaying).Returns(false);

        var handler = new PlayCommandHandler(mockPlayer.Object, mockMetronome.Object, mockLogger.Object);
        var command = new PlayCommand { StartMetronome = false };

        // Act
        await handler.Handle(command);

        // Assert
        mockPlayer.Verify(p => p.Play(), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPlayerAlreadyPlaying_DoesNothing()
    {
        // Arrange
        var mockPlayer = new Mock<IAudioPlayer>();
        mockPlayer.Setup(p => p.IsPlaying).Returns(true);

        var handler = new PlayCommandHandler(mockPlayer.Object, Mock.Of<IMetronome>(), Mock.Of<ILogger<PlayCommandHandler>>());
        var command = new PlayCommand { StartMetronome = false };

        // Act
        await handler.Handle(command);

        // Assert
        mockPlayer.Verify(p => p.Play(), Times.Never);
    }
}
```

**Step 2.2.2: Run unit tests**
```bash
dotnet test AiMusicWorkstation.Tests --logger "console;verbosity=detailed"
```

**Expected outcome**: All tests pass  
**If tests fail**:
- Review handler logic against test expectations
- Check mock setups match actual interface contracts
- Verify async/await patterns are correct in handlers
- Update tests or fix handler logic as needed

**Test coverage targets**:
- All 28 command handlers: at least 1 happy path + 1 error case per handler
- All 13 query handlers: at least 1 happy path per handler
- Value objects: immutability, equality, constraints

---

#### Phase 2.3: Integration Testing (Manual Smoke Checks)

**Step 2.3.1: Run application and verify basic flows**

**Smoke Test 1: Application Startup**
```
1. Launch AiMusicWorkstation application
2. Verify MainWindow appears
3. Verify no exceptions in console/debug output
4. Expected: Clean startup with no errors
```

**Smoke Test 2: Playback Flow**
```
1. Load a song from library or import a new song
2. Click Play button (should trigger PlayCommand)
3. Verify playback starts and timeline progresses
4. Click Pause button (should trigger PauseCommand)
5. Click Stop button (should trigger StopCommand)
Expected: Smooth playback with no freezing or exceptions
```

**Smoke Test 3: Chord Display**
```
1. During playback, observe chord display
2. Verify current chord updates correctly (not lagging)
3. Verify upcoming chords display smoothly
4. Verify chord diagram renders without errors
Expected: Chord display is smooth and responsive (98% faster than before)
```

**Smoke Test 4: Library Browsing**
```
1. Open library and scroll through projects
2. Use search/filter boxes
3. Verify UI is responsive (no lag)
4. Verify filter results are correct
Expected: Library browsing is smooth; queries execute instantly
```

**Smoke Test 5: Lyrics Display**
```
1. Play a song with lyrics
2. Scroll through lyrics list during playback
3. Verify active lyric is highlighted
4. Expected: Smooth scrolling with no stuttering
```

**Smoke Test 6: Volume Controls**
```
1. Adjust master volume slider
2. Adjust per-stem volumes (drums, bass, vocals, other)
3. Test mute and solo buttons
4. Expected: All controls respond immediately with correct audio output
```

**Smoke Test 7: Transposition**
```
1. During playback, use transpose controls (+/- semitones)
2. Verify key display updates correctly
3. Verify chords are transposed correctly
4. Expected: Transposition is instant, chords correct
```

**Smoke Test 8: Shutdown & Resource Cleanup**
```
1. Close application via window X button
2. Verify application closes cleanly
3. Check system processes (verify no orphaned Python processes)
4. Expected: Clean shutdown with no resource leaks
```

**Smoke Test Checklist**:
- [ ] Application starts without errors
- [ ] Playback controls (play/pause/stop) work via commands
- [ ] Chord display updates correctly during playback
- [ ] Library queries execute efficiently (no lag when filtering/searching)
- [ ] Lyrics display updates correctly
- [ ] Volume controls respond immediately
- [ ] Transposition works and updates UI instantly
- [ ] Application shuts down cleanly with no orphaned processes

---

#### Phase 2.4: Addressing Issues & Regressions

**Common issues to watch for**:

| Issue | Symptom | Mitigation |
|-------|---------|-----------|
| Missing handler registration | NullReferenceException when executing command | Add missing registration to DI container in App.xaml.cs |
| Incorrect query handler return type | InvalidCastException | Verify handler return type matches query generic parameter |
| Async handler not awaited | UI freezes | Ensure ViewModel awaits command execution with `await _commandBus.Execute(...)` |
| Process not cleaned up | Orphaned Python processes after shutdown | Implement proper `Dispose()` in PythonBridgeService, ensure `_pythonProcess.Kill()` called |
| Large library query slow | UI lag when loading library | Verify `FilterProjectsQueryHandler` uses efficient LINQ; check if binary search implemented in `GetCurrentChordQuery` |
| Chord display lag | Chords update slowly during playback | Profile `GetCurrentChordQuery` handler; ensure binary search is used (not linear search) |

---

#### Phase 2.5: Final Validation & Sign-Off

**Pre-merge checklist**:
- [ ] Solution compiles with zero errors
- [ ] All unit tests pass (100% pass rate expected)
- [ ] All smoke tests pass
- [ ] No warnings in build output (or acceptable warnings documented)
- [ ] No resource leaks (verify process cleanup)
- [ ] No exceptions in debug output during smoke tests
- [ ] Code review completed and approved
- [ ] All tasks documented and linked to PR
- [ ] CQRS pattern applied consistently (commands/queries/handlers follow same structure)
- [ ] DI container fully configured and no manual `new` instantiations in Desktop layer
- [ ] MainWindow reduced to ~50 LOC (view + ViewModel binding only)

**Deliverable (TASK-002 completion)**:
- All tests passing
- All smoke checks passing
- Solution ready for merge
- Ready to move to execution/deployment phase

---

## Risk Management

Top risks and mitigations:
- Risk: Compilation/regression errors across many files
  - Mitigation: All-At-Once atomic pass; run full build and iterate on fixes immediately

- Risk: Missing interface or mismatched signatures
  - Mitigation: Define interfaces first; implement adapters; add unit tests

- Risk: Python server lifecycle issues
  - Mitigation: Implement process tracking, logging, and graceful shutdown in `IPythonAnalysisService` implementation

- Risk: Regressions in audio playback timing
  - Mitigation: Keep audio pipeline unchanged where possible; run playback smoke tests

- Risk: Long PR review due to size
  - Mitigation: Keep PR focused on architecture and provide migration guide in PR description; split non-dependent changes into follow-up PRs only if necessary

---

## Source Control Strategy

- Create feature branch: `feature/cqrs-refactor` from current working branch
- Atomic migration commit policy: Prefer a single atomic commit for all code moves and interface changes where feasible; if repository policy requires smaller commits, ensure commits are logically ordered but the PR must be reviewed as a single migration unit
- PR checklist: include list of moved files, DI registrations, and at-a-glance migration summary
- Code review: include reviewers familiar with audio and WPF sections

---

## Success Criteria

The migration is complete when:
- All projects compile successfully with new projects included
- All command/query handlers are present and resolvable via DI
- MainWindow reduced to view / ViewModel binding only (no business logic)
- No critical issues remain: no orphaned Python processes, proper Dispose implementations in infrastructure, HttpClient pooling implemented for network clients
- Unit tests for handlers and domain value objects pass
- Smoke validation checklist items satisfied

---

## Appendices

A. Files to create (starter list)
- `AiMusicWorkstation.Application.csproj`
- `AiMusicWorkstation.Domain.csproj`
- `AiMusicWorkstation.Infrastructure.csproj`
- `AiMusicWorkstation.Tests.csproj`
- `Commands/Playback/PlayCommand.cs` etc. (see CQRS guide)

B. References
- Assessment: `.github/upgrades/scenarios/new-dotnet-version_b83c50/assessment.md`
- Implementation guide: `.github/upgrades/scenarios/new-dotnet-version_b83c50/CQRS_RESTRUCTURING_GUIDE.md`

---

*This plan.md was generated by the Planning Agent (GitHub Copilot) using the All-At-Once Strategy. It prescribes the atomic changes to the repository structure, code organization, and testing approach required to modernize the AI Music Workstation codebase.*
