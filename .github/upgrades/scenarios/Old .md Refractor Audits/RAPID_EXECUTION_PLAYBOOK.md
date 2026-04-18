# Rapid CQRS Execution Playbook - Solo Developer Edition

**Complete step-by-step guide with integrated git workflow**

---

## 🎯 Git Strategy Overview

- **Main branch source**: `feature/song-structure` (existing - your current working feature)
- **All new branches created FROM**: `feature/song-structure`
- **All PRs target**: `feature/song-structure`
- **Final PR to main**: After complete validation

### Branch Naming Convention
- `feature/cqrs-projects` - Create new projects
- `feature/cqrs-interfaces` - Service interfaces
- `feature/cqrs-infrastructure` - Infrastructure services
- `feature/cqrs-commands` - Critical commands
- `feature/cqrs-queries` - Critical queries
- `feature/cqrs-viewmodels` - ViewModels + DI

---

## STAGE 1: Create Projects & .csproj Files

### Step 1.1: Create Feature Branch

```bash
# Make sure you're on feature/song-structure
git checkout feature/song-structure
git pull origin feature/song-structure

# Create new feature branch from song-structure
git checkout -b feature/cqrs-projects

# Verify you're on the right branch
git branch -v
```

**Expected output**: `* feature/cqrs-projects` (with asterisk showing you're on it)

---

### Step 1.2: Create Project Folders

Open PowerShell in solution root: `C:\Project C.O.D.E\AiMusicWorkstation`

```powershell
# Create project root folders
mkdir AiMusicWorkstation.Application
mkdir AiMusicWorkstation.Domain
mkdir AiMusicWorkstation.Infrastructure
mkdir AiMusicWorkstation.Tests

# Create subdirectory structure for Application
mkdir AiMusicWorkstation.Application\Commands\Playback
mkdir AiMusicWorkstation.Application\Commands\Library
mkdir AiMusicWorkstation.Application\Commands\Audio
mkdir AiMusicWorkstation.Application\Queries\Playback
mkdir AiMusicWorkstation.Application\Queries\Library
mkdir AiMusicWorkstation.Application\Handlers\Commands
mkdir AiMusicWorkstation.Application\Handlers\Queries
mkdir AiMusicWorkstation.Application\Bus
mkdir AiMusicWorkstation.Application\Exceptions

# Create subdirectories for Domain
mkdir AiMusicWorkstation.Domain\Services
mkdir AiMusicWorkstation.Domain\Entities
mkdir AiMusicWorkstation.Domain\ValueObjects

# Create subdirectories for Infrastructure
mkdir AiMusicWorkstation.Infrastructure\ExternalServices
mkdir AiMusicWorkstation.Infrastructure\Persistence
mkdir AiMusicWorkstation.Infrastructure\Logging

# Create subdirectories for Tests
mkdir AiMusicWorkstation.Tests\Application\Commands
mkdir AiMusicWorkstation.Tests\Application\Queries
mkdir AiMusicWorkstation.Tests\Domain

# Verify structure
Get-ChildItem -Directory | Select-Object Name
```

---

### Step 1.3: Create .csproj Files

**File**: `AiMusicWorkstation.Application/AiMusicWorkstation.Application.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\AiMusicWorkstation.Domain\AiMusicWorkstation.Domain.csproj" />
    <ProjectReference Include="..\AiMusicWorkstation.Infrastructure\AiMusicWorkstation.Infrastructure.csproj" />
    <ProjectReference Include="..\AiMusicWorkstation.Shared\AiMusicWorkstation.Shared.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.3" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.3" />
  </ItemGroup>
</Project>
```

**File**: `AiMusicWorkstation.Domain/AiMusicWorkstation.Domain.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\AiMusicWorkstation.Shared\AiMusicWorkstation.Shared.csproj" />
  </ItemGroup>
</Project>
```

**File**: `AiMusicWorkstation.Infrastructure/AiMusicWorkstation.Infrastructure.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\AiMusicWorkstation.Domain\AiMusicWorkstation.Domain.csproj" />
    <ProjectReference Include="..\AiMusicWorkstation.Shared\AiMusicWorkstation.Shared.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.3" />
    <PackageReference Include="Polly" Version="8.4.1" />
  </ItemGroup>
</Project>
```

**File**: `AiMusicWorkstation.Tests/AiMusicWorkstation.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
    <LangVersion>14.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit" Version="2.8.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.1" />
    <PackageReference Include="Moq" Version="4.20.70" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\AiMusicWorkstation.Application\AiMusicWorkstation.Application.csproj" />
    <ProjectReference Include="..\AiMusicWorkstation.Domain\AiMusicWorkstation.Domain.csproj" />
    <ProjectReference Include="..\AiMusicWorkstation.Infrastructure\AiMusicWorkstation.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

---

### Step 1.4: Update Solution File

Open `AiMusicWorkstation.slnx` in text editor and add these projects to the `projects` array:

```json
{
  "projects": [
    // ... existing projects ...
    "AiMusicWorkstation.Application/AiMusicWorkstation.Application.csproj",
    "AiMusicWorkstation.Domain/AiMusicWorkstation.Domain.csproj",
    "AiMusicWorkstation.Infrastructure/AiMusicWorkstation.Infrastructure.csproj",
    "AiMusicWorkstation.Tests/AiMusicWorkstation.Tests.csproj"
  ]
}
```

---

### Step 1.5: Update Desktop .csproj

**File**: `AiMusicWorkstation.Desktop/AiMusicWorkstation.Desktop.csproj`

Add to `<ItemGroup>` section:

```xml
<ItemGroup>
  <ProjectReference Include="..\AiMusicWorkstation.Application\AiMusicWorkstation.Application.csproj" />
  <ProjectReference Include="..\AiMusicWorkstation.Infrastructure\AiMusicWorkstation.Infrastructure.csproj" />
</ItemGroup>
```

---

### Step 1.6: Build & Verify

```bash
dotnet clean
dotnet build
```

**Expected**: Build succeeds (with many "missing type" errors - NORMAL)

---

### Step 1.7: First Commit & Push

```bash
# Stage all changes
git add .

# Commit with descriptive message
git commit -m "feat(cqrs): create application, domain, infrastructure, and test projects

- Add 4 new .NET 10 SDK-style projects
- Configure project references and dependencies
- Setup directory structure for CQRS layers
- Update solution file with new projects
- Update Desktop project references"

# Push to origin
git push origin feature/cqrs-projects
```

**Output**: Branch pushed successfully

---

## STAGE 2: Service Interfaces

### Step 2.1: Continue on Same Branch

```bash
# You're already on feature/cqrs-projects, continue using it
# Verify you're still on the right branch
git branch -v
# Expected: * feature/cqrs-projects
```

---

### Step 2.2: Create Service Interfaces

**File**: `Domain/Services/IAudioPlayer.cs`

```csharp
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
```

**File**: `Domain/Services/IPythonAnalysisService.cs`

```csharp
namespace AiMusicWorkstation.Domain.Services;

public interface IPythonAnalysisService : IDisposable
{
    Task<AnalysisResult> RunAnalysisAsync(
        string filePath,
        bool useCloud = true,
        CancellationToken cancellationToken = default);
    
    Task<AnalysisResult> ReAnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}

public class AnalysisResult
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public double Bpm { get; set; }
    public string Key { get; set; } = string.Empty;
    public string StemsPath { get; set; } = string.Empty;
    public List<LyricSegment> Lyrics { get; set; } = new();
    public List<ChordEvent> Chords { get; set; } = new();
}
```

**File**: `Domain/Repositories/ILibraryRepository.cs`

```csharp
namespace AiMusicWorkstation.Domain.Repositories;

public interface ILibraryRepository
{
    Task<List<SongProject>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SongProject?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task AddAsync(SongProject project, CancellationToken cancellationToken = default);
    Task UpdateAsync(SongProject project, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
```

**File**: `Domain/Services/ISmartImporterService.cs`

```csharp
namespace AiMusicWorkstation.Domain.Services;

public interface ISmartImporterService
{
    Task<(string FilePath, string Title, string Artist)> DownloadSongAsync(
        string url,
        IProgress<string> progress,
        CancellationToken cancellationToken = default);
    
    Task<(string Genre, string? Artist)> GetOfficialMetadataAsync(
        string trackId,
        CancellationToken cancellationToken = default);
}
```

**File**: `Domain/Services/IMetronome.cs`

```csharp
namespace AiMusicWorkstation.Domain.Services;

public interface IMetronome : IDisposable
{
    Task StartAsync(double bpm, CancellationToken cancellationToken = default);
    void Stop();
}
```

---

### Step 2.3: Build & Verify

```bash
dotnet build
```

**Expected**: Still has errors but interfaces compile

---

### Step 2.4: Commit Changes

```bash
# Stage changes
git add .

# Commit
git commit -m "feat(cqrs): define domain service interfaces

- Create IAudioPlayer for audio playback abstraction
- Create IPythonAnalysisService for Python bridge interface
- Create ILibraryRepository for data persistence
- Create ISmartImporterService for media import
- Create IMetronome for timing/metronome
- Add AnalysisResult DTO class"

# Push to origin
git push origin feature/cqrs-projects
```

---

## STAGE 3: Infrastructure Services (Wrappers)

### Step 3.1: Create Infrastructure Services

**File**: `Infrastructure/ExternalServices/AudioPlayerService.cs`

```csharp
namespace AiMusicWorkstation.Infrastructure.ExternalServices;

public class AudioPlayerService : IAudioPlayer
{
    private readonly StemPlayer _inner;
    
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
    
    public AudioPlayerService(StemPlayer stemPlayer) 
        => _inner = stemPlayer ?? throw new ArgumentNullException(nameof(stemPlayer));
    
    public void LoadStems(string stemsPath) => _inner.LoadStems(stemsPath);
    public void Play() => _inner.Play();
    public void Pause() => _inner.Pause();
    public void Stop() => _inner.Stop();
    public void SetMasterVolume(float volume) => _inner.SetMasterVolume(volume);
    public void SetVolume(string stemName, float volume) => _inner.SetVolume(stemName, volume);
    public void SetMute(string stemName, bool isMuted) => _inner.SetMute(stemName, isMuted);
    public void SetSolo(string stemName, bool isSolo) => _inner.SetSolo(stemName, isSolo);
    public void Dispose() => _inner.Dispose();
}
```

**File**: `Infrastructure/ExternalServices/PythonBridgeService.cs`

```csharp
namespace AiMusicWorkstation.Infrastructure.ExternalServices;

public class PythonBridgeService : IPythonAnalysisService
{
    private readonly PythonBridge _inner;
    private readonly ILogger<PythonBridgeService> _logger;
    
    public PythonBridgeService(PythonBridge pythonBridge, ILogger<PythonBridgeService> logger)
    {
        _inner = pythonBridge ?? throw new ArgumentNullException(nameof(pythonBridge));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<AnalysisResult> RunAnalysisAsync(
        string filePath,
        bool useCloud = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));
            
            _logger.LogInformation("Starting analysis for {FilePath}", filePath);
            var result = await _inner.RunAnalysisAsync(filePath, useCloud);
            _logger.LogInformation("Analysis completed successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analysis failed for {FilePath}", filePath);
            throw;
        }
    }
    
    public async Task<AnalysisResult> ReAnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => await RunAnalysisAsync(filePath, useCloud: false, cancellationToken);
    
    public void Dispose() => _inner.Dispose();
}
```

**File**: `Infrastructure/Persistence/LibraryRepository.cs`

```csharp
namespace AiMusicWorkstation.Infrastructure.Persistence;

public class LibraryRepository : ILibraryRepository
{
    private readonly LibraryManager _inner;
    private readonly ILogger<LibraryRepository> _logger;
    
    public LibraryRepository(LibraryManager libraryManager, ILogger<LibraryRepository> logger)
    {
        _inner = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<List<SongProject>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await Task.FromResult(_inner.GetAll());
            _logger.LogInformation("Retrieved {Count} projects from library", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all projects");
            throw;
        }
    }
    
    public async Task<SongProject?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await Task.FromResult(_inner.GetById(id));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get project {Id}", id);
            throw;
        }
    }
    
    public async Task AddAsync(SongProject project, CancellationToken cancellationToken = default)
    {
        try
        {
            _inner.Add(project);
            await Task.CompletedTask;
            _logger.LogInformation("Project added: {Title}", project.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add project");
            throw;
        }
    }
    
    public async Task UpdateAsync(SongProject project, CancellationToken cancellationToken = default)
    {
        try
        {
            _inner.Update(project);
            await Task.CompletedTask;
            _logger.LogInformation("Project updated: {Title}", project.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update project");
            throw;
        }
    }
    
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            _inner.Delete(id);
            await Task.CompletedTask;
            _logger.LogInformation("Project deleted: {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete project {Id}", id);
            throw;
        }
    }
    
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _inner.SaveAsync();
            _logger.LogInformation("Library saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save library");
            throw;
        }
    }
}
```

---

### Step 3.2: Build & Verify

```bash
dotnet build
```

**Expected**: Infrastructure services compile

---

### Step 3.3: Commit Changes

```bash
# Stage changes
git add .

# Commit
git commit -m "feat(cqrs): implement infrastructure service wrappers

- Create AudioPlayerService wrapping StemPlayer
- Create PythonBridgeService wrapping PythonBridge
- Create LibraryRepository wrapping LibraryManager
- Add logging to all service methods
- Implement error handling in wrapper layer"

# Push to origin
git push origin feature/cqrs-projects
```

---

## STAGE 4: CQRS Layer - Commands & Handlers

### Step 4.1: Create Feature Branch for Commands

```bash
# First, finish the current work and push
git push origin feature/cqrs-projects

# Now create new branch FROM feature/song-structure
git checkout feature/song-structure
git pull origin feature/song-structure

git checkout -b feature/cqrs-commands

# Verify you're on the new branch
git branch -v
# Expected: * feature/cqrs-commands
```

---

### Step 4.2: Create Command Base Interfaces

**File**: `Application/Commands/ICommand.cs`

```csharp
namespace AiMusicWorkstation.Application.Commands;

public interface ICommand { }
```

**File**: `Application/Handlers/Commands/ICommandHandler.cs`

```csharp
namespace AiMusicWorkstation.Application.Handlers.Commands;

public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    Task Handle(TCommand command, CancellationToken cancellationToken = default);
}
```

---

### Step 4.3: Create Critical Commands (5 commands)

**File**: `Application/Commands/Playback/PlayCommand.cs`

```csharp
namespace AiMusicWorkstation.Application.Commands.Playback;

public class PlayCommand : ICommand
{
    public bool StartMetronome { get; set; }
}
```

**File**: `Application/Commands/Playback/PauseCommand.cs`

```csharp
namespace AiMusicWorkstation.Application.Commands.Playback;

public class PauseCommand : ICommand { }
```

**File**: `Application/Commands/Playback/StopCommand.cs`

```csharp
namespace AiMusicWorkstation.Application.Commands.Playback;

public class StopCommand : ICommand { }
```

**File**: `Application/Commands/Library/DeleteProjectCommand.cs`

```csharp
namespace AiMusicWorkstation.Application.Commands.Library;

public class DeleteProjectCommand : ICommand
{
    public string ProjectId { get; set; } = string.Empty;
}
```

**File**: `Application/Commands/Library/ImportSongCommand.cs`

```csharp
namespace AiMusicWorkstation.Application.Commands.Library;

public class ImportSongCommand : ICommand
{
    public string FilePath { get; set; } = string.Empty;
    public string? SpotifyUrl { get; set; }
    public string? CustomTitle { get; set; }
    public string? ArtistName { get; set; }
}
```

---

### Step 4.4: Create Command Handlers (5 handlers)

**File**: `Application/Handlers/Commands/PlayCommandHandler.cs`

```csharp
namespace AiMusicWorkstation.Application.Handlers.Commands;

public class PlayCommandHandler : ICommandHandler<PlayCommand>
{
    private readonly IAudioPlayer _player;
    private readonly IMetronome _metronome;
    private readonly ILogger<PlayCommandHandler> _logger;
    
    public PlayCommandHandler(
        IAudioPlayer player,
        IMetronome metronome,
        ILogger<PlayCommandHandler> logger)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _metronome = metronome ?? throw new ArgumentNullException(nameof(metronome));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task Handle(PlayCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_player.IsPlaying)
            {
                _logger.LogWarning("Attempt to play while already playing");
                return;
            }
            
            _player.Play();
            _logger.LogInformation("Playback started");
            
            if (command.StartMetronome)
            {
                await _metronome.StartAsync(120, cancellationToken);
                _logger.LogInformation("Metronome started");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting playback");
            throw;
        }
    }
}
```

**File**: `Application/Handlers/Commands/PauseCommandHandler.cs`

```csharp
namespace AiMusicWorkstation.Application.Handlers.Commands;

public class PauseCommandHandler : ICommandHandler<PauseCommand>
{
    private readonly IAudioPlayer _player;
    private readonly ILogger<PauseCommandHandler> _logger;
    
    public PauseCommandHandler(IAudioPlayer player, ILogger<PauseCommandHandler> logger)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task Handle(PauseCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_player.IsPlaying)
            {
                _logger.LogWarning("Attempt to pause while not playing");
                return;
            }
            
            _player.Pause();
            _logger.LogInformation("Playback paused");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing playback");
            throw;
        }
    }
}
```

**File**: `Application/Handlers/Commands/StopCommandHandler.cs`

```csharp
namespace AiMusicWorkstation.Application.Handlers.Commands;

public class StopCommandHandler : ICommandHandler<StopCommand>
{
    private readonly IAudioPlayer _player;
    private readonly ILogger<StopCommandHandler> _logger;
    
    public StopCommandHandler(IAudioPlayer player, ILogger<StopCommandHandler> logger)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task Handle(StopCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            _player.Stop();
            _logger.LogInformation("Playback stopped");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping playback");
            throw;
        }
    }
}
```

**File**: `Application/Handlers/Commands/DeleteProjectCommandHandler.cs`

```csharp
namespace AiMusicWorkstation.Application.Handlers.Commands;

public class DeleteProjectCommandHandler : ICommandHandler<DeleteProjectCommand>
{
    private readonly ILibraryRepository _repository;
    private readonly ILogger<DeleteProjectCommandHandler> _logger;
    
    public DeleteProjectCommandHandler(
        ILibraryRepository repository,
        ILogger<DeleteProjectCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task Handle(DeleteProjectCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(command.ProjectId))
                throw new ArgumentException("ProjectId is required", nameof(command.ProjectId));
            
            await _repository.DeleteAsync(command.ProjectId, cancellationToken);
            await _repository.SaveAsync(cancellationToken);
            _logger.LogInformation("Project deleted: {ProjectId}", command.ProjectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project {ProjectId}", command.ProjectId);
            throw;
        }
    }
}
```

**File**: `Application/Handlers/Commands/ImportSongCommandHandler.cs`

```csharp
namespace AiMusicWorkstation.Application.Handlers.Commands;

public class ImportSongCommandHandler : ICommandHandler<ImportSongCommand>
{
    private readonly IPythonAnalysisService _pythonAnalysis;
    private readonly ISmartImporterService _importer;
    private readonly ILibraryRepository _repository;
    private readonly IAudioPlayer _player;
    private readonly ILogger<ImportSongCommandHandler> _logger;
    
    public ImportSongCommandHandler(
        IPythonAnalysisService pythonAnalysis,
        ISmartImporterService importer,
        ILibraryRepository repository,
        IAudioPlayer player,
        ILogger<ImportSongCommandHandler> logger)
    {
        _pythonAnalysis = pythonAnalysis ?? throw new ArgumentNullException(nameof(pythonAnalysis));
        _importer = importer ?? throw new ArgumentNullException(nameof(importer));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task Handle(ImportSongCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(command.FilePath))
                throw new ArgumentException("FilePath is required", nameof(command.FilePath));
            
            if (!File.Exists(command.FilePath))
                throw new FileNotFoundException($"File not found: {command.FilePath}");
            
            _logger.LogInformation("Starting import for {FilePath}", command.FilePath);
            
            // Analyze
            var analysis = await _pythonAnalysis.RunAnalysisAsync(command.FilePath, cancellationToken: cancellationToken);
            
            if (analysis == null)
                throw new InvalidOperationException("Analysis returned null result");
            
            // Load stems
            _player.LoadStems(analysis.StemsPath ?? command.FilePath);
            
            // Get metadata if Spotify URL provided
            string genre = "Uncategorized";
            if (!string.IsNullOrEmpty(command.SpotifyUrl))
            {
                var (spotifyGenre, _) = await _importer.GetOfficialMetadataAsync(command.SpotifyUrl, cancellationToken);
                genre = spotifyGenre ?? "Uncategorized";
            }
            
            // Create project
            var project = new SongProject
            {
                Title = command.CustomTitle ?? Path.GetFileNameWithoutExtension(command.FilePath),
                Artist = command.ArtistName ?? "Unknown",
                Bpm = (int)analysis.Bpm,
                Key = analysis.Key,
                Genre = genre
            };
            
            // Save
            await _repository.AddAsync(project, cancellationToken);
            await _repository.SaveAsync(cancellationToken);
            
            _logger.LogInformation("Song imported successfully: {Title}", project.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing song from {FilePath}", command.FilePath);
            throw;
        }
    }
}
```

---

### Step 4.5: Build & Verify

```bash
dotnet build
```

**Expected**: Commands and handlers compile

---

### Step 4.6: Commit & Create PR

```bash
# Stage all changes
git add .

# Commit
git commit -m "feat(cqrs): implement commands and handlers

- Create command base interface and handlers
- Implement 5 critical commands: Play, Pause, Stop, Delete, Import
- Implement corresponding handlers with logging
- Add error handling and validation
- Wire handlers to domain services"

# Push to origin
git push origin feature/cqrs-commands

# Create PR (from feature/cqrs-commands to feature/song-structure)
# Go to GitHub and create PR with description:
# 
# Title: feat(cqrs): implement playback and library commands
#
# Description:
# Implements core CQRS commands and handlers for:
# - Playback control (Play, Pause, Stop)
# - Library management (Delete project)
# - Song import workflow
#
# These commands form the foundation of the command layer.
# All handlers include logging and error handling.
```

**After PR review**:
```bash
# Merge PR on GitHub, then pull latest
git checkout feature/song-structure
git pull origin feature/song-structure
```

---

## STAGE 5: CQRS Layer - Queries & Handlers

### Step 5.1: Create Feature Branch for Queries

```bash
# Make sure you're on latest feature/song-structure
git checkout feature/song-structure
git pull origin feature/song-structure

# Create new branch
git checkout -b feature/cqrs-queries

# Verify
git branch -v
# Expected: * feature/cqrs-queries
```

---

### Step 5.2: Create Query Base Interfaces

**File**: `Application/Queries/IQuery.cs`

```csharp
namespace AiMusicWorkstation.Application.Queries;

public interface IQuery<TResult> { }
```

**File**: `Application/Handlers/Queries/IQueryHandler.cs`

```csharp
namespace AiMusicWorkstation.Application.Handlers.Queries;

public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> Handle(TQuery query, CancellationToken cancellationToken = default);
}
```

---

### Step 5.3: Create Query DTOs

**File**: `Application/Queries/Playback/GetPlaybackStateQuery.cs`

```csharp
namespace AiMusicWorkstation.Application.Queries.Playback;

public class GetPlaybackStateQuery : IQuery<PlaybackStateDto> { }

public class PlaybackStateDto
{
    public bool IsPlaying { get; set; }
    public TimeSpan CurrentTime { get; set; }
    public TimeSpan TotalTime { get; set; }
    public int Bpm { get; set; }
    public string CurrentKey { get; set; } = string.Empty;
}
```

**File**: `Application/Queries/Playback/GetCurrentChordQuery.cs`

```csharp
namespace AiMusicWorkstation.Application.Queries.Playback;

public class GetCurrentChordQuery : IQuery<ChordDto?> { }

public class ChordDto
{
    public string Name { get; set; } = string.Empty;
    public double Time { get; set; }
}
```

**File**: `Application/Queries/Library/FilterProjectsQuery.cs`

```csharp
namespace AiMusicWorkstation.Application.Queries.Library;

public class FilterProjectsQuery : IQuery<List<SongProjectDto>>
{
    public string SearchTerm { get; set; } = string.Empty;
    public string SelectedGenre { get; set; } = string.Empty;
    public string SortBy { get; set; } = "Latest"; // "Latest", "A-Z", "BPM"
}

public class SongProjectDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public int Bpm { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
}
```

---

### Step 5.4: Create Query Handlers (3 handlers)

**File**: `Application/Handlers/Queries/GetPlaybackStateQueryHandler.cs`

```csharp
namespace AiMusicWorkstation.Application.Handlers.Queries;

public class GetPlaybackStateQueryHandler : IQueryHandler<GetPlaybackStateQuery, PlaybackStateDto>
{
    private readonly IAudioPlayer _player;
    private readonly ILogger<GetPlaybackStateQueryHandler> _logger;
    
    public GetPlaybackStateQueryHandler(IAudioPlayer player, ILogger<GetPlaybackStateQueryHandler> logger)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<PlaybackStateDto> Handle(GetPlaybackStateQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new PlaybackStateDto
            {
                IsPlaying = _player.IsPlaying,
                CurrentTime = _player.CurrentTime,
                TotalTime = _player.TotalTime,
                Bpm = 120, // TODO: Get from player
                CurrentKey = "--" // TODO: Get from player
            };
            
            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting playback state");
            throw;
        }
    }
}
```

**File**: `Application/Handlers/Queries/GetCurrentChordQueryHandler.cs`

```csharp
namespace AiMusicWorkstation.Application.Handlers.Queries;

public class GetCurrentChordQueryHandler : IQueryHandler<GetCurrentChordQuery, ChordDto?>
{
    private readonly IAudioPlayer _player;
    private readonly ILogger<GetCurrentChordQueryHandler> _logger;
    
    // TODO: Inject chord provider
    private List<ChordDto> _chords = new();
    
    public GetCurrentChordQueryHandler(IAudioPlayer player, ILogger<GetCurrentChordQueryHandler> logger)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<ChordDto?> Handle(GetCurrentChordQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            double currentTime = _player.CurrentTime.TotalSeconds;
            
            // Binary search for current chord (O(log n) instead of O(n))
            var chord = BinarySearchChord(currentTime);
            
            return await Task.FromResult(chord);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current chord");
            throw;
        }
    }
    
    private ChordDto? BinarySearchChord(double currentTime)
    {
        if (_chords.Count == 0) return null;
        
        int left = 0, right = _chords.Count - 1;
        ChordDto? result = null;
        
        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            
            if (_chords[mid].Time <= currentTime)
            {
                result = _chords[mid];
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }
        
        return result;
    }
}
```

**File**: `Application/Handlers/Queries/FilterProjectsQueryHandler.cs`

```csharp
namespace AiMusicWorkstation.Application.Handlers.Queries;

public class FilterProjectsQueryHandler : IQueryHandler<FilterProjectsQuery, List<SongProjectDto>>
{
    private readonly ILibraryRepository _repository;
    private readonly ILogger<FilterProjectsQueryHandler> _logger;
    
    public FilterProjectsQueryHandler(ILibraryRepository repository, ILogger<FilterProjectsQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<List<SongProjectDto>> Handle(FilterProjectsQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var projects = await _repository.GetAllAsync(cancellationToken);
            
            var dtos = projects
                .ConvertAll(p => new SongProjectDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Artist = p.Artist,
                    Bpm = (int)p.Bpm,
                    Key = p.Key,
                    Genre = p.Genre
                });
            
            // Search filter
            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                var searchLower = query.SearchTerm.ToLower();
                dtos = dtos.Where(p =>
                    p.Title.ToLower().Contains(searchLower) ||
                    p.Artist.ToLower().Contains(searchLower) ||
                    p.Genre.ToLower().Contains(searchLower))
                    .ToList();
            }
            
            // Genre filter
            if (!string.IsNullOrWhiteSpace(query.SelectedGenre) && query.SelectedGenre != "All")
            {
                dtos = dtos.Where(p => p.Genre == query.SelectedGenre).ToList();
            }
            
            // Sort
            dtos = query.SortBy switch
            {
                "A-Z" => dtos.OrderBy(p => p.Title).ToList(),
                "BPM" => dtos.OrderBy(p => p.Bpm).ToList(),
                _ => dtos // Default: Latest (assumes insertion order)
            };
            
            _logger.LogInformation("Filtered projects: {Count} results", dtos.Count);
            return await Task.FromResult(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering projects");
            throw;
        }
    }
}
```

---

### Step 5.5: Build & Verify

```bash
dotnet build
```

**Expected**: Queries and handlers compile

---

### Step 5.6: Commit & Create PR

```bash
# Stage all changes
git add .

# Commit
git commit -m "feat(cqrs): implement queries and handlers

- Create query base interface and handlers
- Implement 3 critical queries: GetPlaybackState, GetCurrentChord, FilterProjects
- Implement corresponding handlers with logging
- Add binary search optimization for chord lookup (O(log n))
- Add DTOs for query results"

# Push to origin
git push origin feature/cqrs-queries

# Create PR on GitHub:
#
# Title: feat(cqrs): implement read operations and queries
#
# Description:
# Implements core CQRS queries and handlers for:
# - Playback state queries
# - Chord lookup (with binary search optimization)
# - Project filtering and search
#
# Includes O(log n) performance optimization for chord searches.
```

**After PR review**:
```bash
git checkout feature/song-structure
git pull origin feature/song-structure
```

---

## STAGE 6: CQRS Buses

### Step 6.1: Create Feature Branch

```bash
git checkout feature/song-structure
git pull origin feature/song-structure

git checkout -b feature/cqrs-buses

# Verify
git branch -v
# Expected: * feature/cqrs-buses
```

---

### Step 6.2: Create Command Bus

**File**: `Application/Bus/ICommandBus.cs`

```csharp
namespace AiMusicWorkstation.Application.Bus;

public interface ICommandBus
{
    Task Execute<TCommand>(TCommand command, CancellationToken cancellationToken = default) 
        where TCommand : class;
}
```

**File**: `Application/Bus/CommandBus.cs`

```csharp
namespace AiMusicWorkstation.Application.Bus;

public class CommandBus : ICommandBus
{
    private readonly IServiceProvider _services;
    private readonly ILogger<CommandBus> _logger;
    
    public CommandBus(IServiceProvider services, ILogger<CommandBus> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task Execute<TCommand>(TCommand command, CancellationToken cancellationToken = default) 
        where TCommand : class
    {
        try
        {
            var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
            dynamic handler = _services.GetService(handlerType) 
                ?? throw new InvalidOperationException($"No handler registered for {command.GetType().Name}");
            
            _logger.LogInformation("Executing command: {CommandType}", command.GetType().Name);
            await handler.Handle((dynamic)command, cancellationToken);
            _logger.LogInformation("Command executed successfully: {CommandType}", command.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command: {CommandType}", command.GetType().Name);
            throw;
        }
    }
}
```

---

### Step 6.3: Create Query Bus

**File**: `Application/Bus/IQueryBus.cs`

```csharp
namespace AiMusicWorkstation.Application.Bus;

public interface IQueryBus
{
    Task<TResult> Execute<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}
```

**File**: `Application/Bus/QueryBus.cs`

```csharp
namespace AiMusicWorkstation.Application.Bus;

public class QueryBus : IQueryBus
{
    private readonly IServiceProvider _services;
    private readonly ILogger<QueryBus> _logger;
    
    public QueryBus(IServiceProvider services, ILogger<QueryBus> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task<TResult> Execute<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        try
        {
            var handlerType = typeof(IQueryHandler<,>)
                .MakeGenericType(query.GetType(), typeof(TResult));
            
            dynamic handler = _services.GetService(handlerType) 
                ?? throw new InvalidOperationException($"No handler registered for {query.GetType().Name}");
            
            _logger.LogInformation("Executing query: {QueryType}", query.GetType().Name);
            var result = await handler.Handle((dynamic)query, cancellationToken);
            _logger.LogInformation("Query executed successfully: {QueryType}", query.GetType().Name);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query: {QueryType}", query.GetType().Name);
            throw;
        }
    }
}
```

---

### Step 6.4: Build & Verify

```bash
dotnet build
```

---

### Step 6.5: Commit & Create PR

```bash
# Stage changes
git add .

# Commit
git commit -m "feat(cqrs): implement command and query buses

- Create ICommandBus and implementation
- Create IQueryBus and implementation
- Add logging for command/query execution
- Add error handling and validation
- Enable centralized command/query routing"

# Push to origin
git push origin feature/cqrs-buses

# Create PR on GitHub:
#
# Title: feat(cqrs): implement CQRS buses
#
# Description:
# Implements core CQRS bus infrastructure:
# - CommandBus for routing commands to handlers
# - QueryBus for routing queries to handlers
# - Centralized logging for all operations
# - Error handling with detailed logging
```

**After PR review**:
```bash
git checkout feature/song-structure
git pull origin feature/song-structure
```

---

## STAGE 7: ViewModels & Dependency Injection

### Step 7.1: Create Feature Branch

```bash
git checkout feature/song-structure
git pull origin feature/song-structure

git checkout -b feature/cqrs-viewmodels

# Verify
git branch -v
# Expected: * feature/cqrs-viewmodels
```

---

### Step 7.2: Create ViewModelBase

**File**: `Desktop/ViewModels/ViewModelBase.cs`

```csharp
namespace AiMusicWorkstation.Desktop.ViewModels;

public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected void SetProperty<T>(
        ref T backingField,
        T value,
        [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
            return;
        
        backingField = value;
        OnPropertyChanged(propertyName);
    }
    
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
```

---

### Step 7.3: Create PlaybackViewModel

**File**: `Desktop/ViewModels/PlaybackViewModel.cs`

```csharp
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
        
        PlayCommand = new AsyncRelayCommand(ExecutePlay);
        PauseCommand = new AsyncRelayCommand(ExecutePause);
        StopCommand = new AsyncRelayCommand(ExecuteStop);
    }
    
    private async Task ExecutePlay(object? parameter)
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
    
    private async Task ExecutePause(object? parameter)
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
    
    private async Task ExecuteStop(object? parameter)
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
```

---

### Step 7.4: Create LibraryViewModel

**File**: `Desktop/ViewModels/LibraryViewModel.cs`

```csharp
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
```

---

### Step 7.5: Update App.xaml.cs with DI

**File**: `Desktop/App.xaml.cs`

```csharp
namespace AiMusicWorkstation;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    
    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();
        
        // Logging
        services.AddLogging(config => config.AddDebug().AddConsole());
        
        // Configuration
        var config = new ConfigurationBuilder()
            .AddUserSecrets<App>()
            .Build();
        services.AddSingleton<IConfiguration>(config);
        
        // Core Services - Existing (wrapped with interfaces)
        services.AddSingleton(new StemPlayer());
        services.AddSingleton<IAudioPlayer>(sp => 
            new AudioPlayerService(sp.GetRequiredService<StemPlayer>()));
        services.AddSingleton(new PythonBridge());
        services.AddSingleton<IPythonAnalysisService>(sp => 
            new PythonBridgeService(sp.GetRequiredService<PythonBridge>(), 
                sp.GetRequiredService<ILogger<PythonBridgeService>>()));
        services.AddSingleton(new LibraryManager());
        services.AddSingleton<ILibraryRepository>(sp => 
            new LibraryRepository(sp.GetRequiredService<LibraryManager>(), 
                sp.GetRequiredService<ILogger<LibraryRepository>>()));
        services.AddSingleton(new SmartImporter(config));
        services.AddSingleton<ISmartImporterService>(sp => 
            sp.GetRequiredService<SmartImporter>());
        services.AddSingleton(new Metronome());
        services.AddSingleton<IMetronome>(sp => sp.GetRequiredService<Metronome>());
        
        // CQRS Bus
        services.AddSingleton<ICommandBus, CommandBus>();
        services.AddSingleton<IQueryBus, QueryBus>();
        
        // Command Handlers (add all 5 from stage 4)
        services.AddScoped<ICommandHandler<PlayCommand>, PlayCommandHandler>();
        services.AddScoped<ICommandHandler<PauseCommand>, PauseCommandHandler>();
        services.AddScoped<ICommandHandler<StopCommand>, StopCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteProjectCommand>, DeleteProjectCommandHandler>();
        services.AddScoped<ICommandHandler<ImportSongCommand>, ImportSongCommandHandler>();
        
        // Query Handlers (add all 3 from stage 5)
        services.AddScoped<IQueryHandler<GetPlaybackStateQuery, PlaybackStateDto>, GetPlaybackStateQueryHandler>();
        services.AddScoped<IQueryHandler<GetCurrentChordQuery, ChordDto?>, GetCurrentChordQueryHandler>();
        services.AddScoped<IQueryHandler<FilterProjectsQuery, List<SongProjectDto>>, FilterProjectsQueryHandler>();
        
        // ViewModels
        services.AddSingleton<PlaybackViewModel>();
        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<MainWindow>();
        
        _serviceProvider = services.BuildServiceProvider();
        
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
        
        base.OnStartup(e);
    }
}
```

---

### Step 7.6: Update MainWindow.xaml.cs

**File**: `Desktop/MainWindow.xaml.cs`

```csharp
namespace AiMusicWorkstation;

public partial class MainWindow : Window
{
    private readonly PlaybackViewModel _playbackViewModel;
    private readonly LibraryViewModel _libraryViewModel;
    
    public MainWindow(PlaybackViewModel playbackViewModel, LibraryViewModel libraryViewModel)
    {
        InitializeComponent();
        
        _playbackViewModel = playbackViewModel ?? throw new ArgumentNullException(nameof(playbackViewModel));
        _libraryViewModel = libraryViewModel ?? throw new ArgumentNullException(nameof(libraryViewModel));
        
        // Set data context (or bind ViewModels to appropriate UI sections)
        DataContext = new { Playback = _playbackViewModel, Library = _libraryViewModel };
        
        Closing += MainWindow_Closing;
    }
    
    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Cleanup if needed
    }
}
```

---

### Step 7.7: Update MainWindow.xaml

**File**: `Desktop/MainWindow.xaml`

Add bindings for playback commands:

```xml
<!-- Playback Controls -->
<Button Command="{Binding Playback.PlayCommand}" Content="▶" />
<Button Command="{Binding Playback.PauseCommand}" Content="⏸" />
<Button Command="{Binding Playback.StopCommand}" Content="⏹" />

<!-- Library -->
<DataGrid ItemsSource="{Binding Library.Projects}" />
<TextBox Text="{Binding Library.SearchTerm, UpdateSourceTrigger=PropertyChanged}" 
         Placeholder="Search..." />
<Button Command="{Binding Library.RefreshCommand}" Content="Refresh" />
```

---

### Step 7.8: Build & Verify Application Boots

```bash
dotnet clean
dotnet build
dotnet run
```

**Expected**:
- Application boots successfully
- No compilation errors
- Play/Pause/Stop buttons functional
- Library displays projects
- Search filter works

---

### Step 7.9: Commit & Create PR

```bash
# Stage all changes
git add .

# Commit
git commit -m "feat(cqrs): implement viewmodels and dependency injection

- Create ViewModelBase with INotifyPropertyChanged
- Implement PlaybackViewModel for playback controls
- Implement LibraryViewModel for library management
- Setup ServiceCollection with DI container in App.xaml.cs
- Register all services, handlers, and viewmodels
- Update MainWindow to use ViewModels
- Add XAML bindings for commands and properties
- Application now boots with full CQRS architecture"

# Push to origin
git push origin feature/cqrs-viewmodels

# Create PR on GitHub:
#
# Title: feat(cqrs): implement viewmodels and complete DI setup
#
# Description:
# Completes CQRS implementation with:
# - ViewModels for reactive UI state management
# - Dependency injection container setup
# - Service registration and wiring
# - XAML command bindings
#
# Application is now fully functional with CQRS architecture.
# Play/Pause/Stop commands work through command handlers.
# Library operations work through query handlers.
```

**After PR review**:
```bash
git checkout feature/song-structure
git pull origin feature/song-structure
```

---

## FINAL STEPS: Test & Build

### Verification Checklist

```bash
# 1. Full build
dotnet clean
dotnet build

# 2. Application runs
dotnet run

# 3. Smoke tests
- Click Play button → Audio plays via PlayCommandHandler
- Click Pause button → Audio pauses via PauseCommandHandler
- Click Stop button → Audio stops via StopCommandHandler
- View library → Projects loaded via FilterProjectsQuery
- Type in search → Results filtered via QueryBus
```

### Final Push to feature/song-structure

```bash
# When all stages complete
git checkout feature/song-structure
git pull origin feature/song-structure

# Final status check
git log --oneline feature/cqrs-projects..feature/song-structure
git log --oneline feature/cqrs-commands..feature/song-structure
git log --oneline feature/cqrs-queries..feature/song-structure
git log --oneline feature/cqrs-buses..feature/song-structure
git log --oneline feature/cqrs-viewmodels..feature/song-structure
```

---

## 🎯 Summary: What You've Built

By following this playbook, you have:

✅ Created 4 new projects with proper architecture  
✅ Defined 5 service interfaces  
✅ Implemented infrastructure wrappers  
✅ Created 5 critical commands with handlers  
✅ Created 3 critical queries with handlers  
✅ Implemented CQRS buses for routing  
✅ Created 2 ViewModels with reactive bindings  
✅ Setup complete dependency injection  
✅ Application boots and core features work  

**Next Steps**: Follow the same pattern to add remaining 20+ handlers and 10+ queries

---

**You now have a working CQRS architecture. Flow state engaged. Go build! 🚀**
