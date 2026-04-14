# TASK-001 Complete File Manifest

## What Gets Created (Atomic TASK-001)

This document is a quick reference for **exactly which files** are created and **where they go** during TASK-001.

---

## NEW PROJECTS (4 total)

### `AiMusicWorkstation.Application/` (28 new files)

**Commands/** (15 command classes + base interface)
```
Commands/
├── ICommand.cs                          (base interface)
├── Playback/
│   ├── PlayCommand.cs
│   ├── PauseCommand.cs
│   ├── StopCommand.cs
│   ├── SetTransposeCommand.cs
│   └── SaveLyricsCommand.cs
├── Library/
│   ├── ImportSongCommand.cs
│   ├── DownloadSongCommand.cs
│   ├── DeleteProjectCommand.cs
│   ├── RenameProjectCommand.cs
│   ├── MoveToGroupCommand.cs
│   ├── RefreshMetadataCommand.cs
│   └── RefreshMetadataCommand.cs
└── Audio/
    ├── SetVolumeCommand.cs
    ├── SetMuteCommand.cs
    └── SetSoloCommand.cs
```

**Queries/** (13 query classes + base interface)
```
Queries/
├── IQuery.cs                            (base interface)
├── Playback/
│   ├── GetPlaybackStateQuery.cs
│   ├── GetCurrentChordQuery.cs
│   ├── GetUpcomingChordsQuery.cs
│   ├── GetActiveLyricsQuery.cs
│   ├── GetCurrentTimeQuery.cs
│   └── GetCurrentKeyQuery.cs
├── Library/
│   ├── GetAllProjectsQuery.cs
│   ├── GetProjectByIdQuery.cs
│   ├── FilterProjectsQuery.cs
│   ├── SearchProjectsQuery.cs
│   ├── GetGenresQuery.cs
│   └── GetSortedProjectsQuery.cs
└── Analysis/
    └── GetAnalysisResultQuery.cs
```

**Handlers/** (28 handler classes + 2 base interfaces)
```
Handlers/
├── Commands/
│   ├── ICommandHandler.cs               (base interface)
│   ├── PlayCommandHandler.cs
│   ├── PauseCommandHandler.cs
│   ├── StopCommandHandler.cs
│   ├── SetTransposeCommandHandler.cs
│   ├── SaveLyricsCommandHandler.cs
│   ├── ImportSongCommandHandler.cs
│   ├── DownloadSongCommandHandler.cs
│   ├── DeleteProjectCommandHandler.cs
│   ├── RenameProjectCommandHandler.cs
│   ├── MoveToGroupCommandHandler.cs
│   ├── RefreshMetadataCommandHandler.cs
│   ├── SetVolumeCommandHandler.cs
│   ├── SetMuteCommandHandler.cs
│   └── SetSoloCommandHandler.cs
└── Queries/
    ├── IQueryHandler.cs                 (base interface)
    ├── GetPlaybackStateQueryHandler.cs
    ├── GetCurrentChordQueryHandler.cs
    ├── GetUpcomingChordsQueryHandler.cs
    ├── GetActiveLyricsQueryHandler.cs
    ├── GetCurrentTimeQueryHandler.cs
    ├── GetCurrentKeyQueryHandler.cs
    ├── GetAllProjectsQueryHandler.cs
    ├── GetProjectByIdQueryHandler.cs
    ├── FilterProjectsQueryHandler.cs
    ├── SearchProjectsQueryHandler.cs
    ├── GetGenresQueryHandler.cs
    └── GetSortedProjectsQueryHandler.cs
```

**Bus/** (2 bus interfaces + 2 implementations)
```
Bus/
├── ICommandBus.cs
├── CommandBus.cs
├── IQueryBus.cs
└── QueryBus.cs
```

**Exceptions/** (3 exception classes)
```
Exceptions/
├── CommandExecutionException.cs
├── QueryExecutionException.cs
└── ValidationException.cs
```

**Total Application files**: 28 files (commands + queries + handlers + bus + exceptions)

---

### `AiMusicWorkstation.Domain/` (11 new files)

**Services/** (5 service interfaces)
```
Services/
├── IAudioPlayer.cs
├── IPythonAnalysisService.cs
├── ILibraryRepository.cs
├── ISmartImporterService.cs
└── IMetronome.cs
```

**Entities/** (3 entity classes)
```
Entities/
├── SongProjectEntity.cs
├── PlaybackStateEntity.cs
└── AnalysisEntity.cs
```

**ValueObjects/** (3 immutable value objects)
```
ValueObjects/
├── Key.cs
├── Chord.cs
└── Volume.cs
```

**Total Domain files**: 11 files

---

### `AiMusicWorkstation.Infrastructure/` (6 new files)

**ExternalServices/** (4 service implementations)
```
ExternalServices/
├── PythonBridgeService.cs              (from PythonBridge.cs, implements IPythonAnalysisService)
├── AudioPlayerService.cs              (wrapper, implements IAudioPlayer)
├── SmartImporterService.cs            (from SmartImporter.cs, implements ISmartImporterService)
└── MetronomeService.cs                (wrapper, implements IMetronome)
```

**Persistence/** (1 repository implementation)
```
Persistence/
└── LibraryRepository.cs               (from LibraryManager.cs, implements ILibraryRepository)
```

**Logging/** (1 logging extension - optional)
```
Logging/
└── LoggerExtensions.cs                (optional helpers)
```

**Total Infrastructure files**: 6 files (services + repository + logging)

---

### `AiMusicWorkstation.Tests/` (6 new files - minimum)

**Application/** (test namespace)
```
Application/
├── Commands/
│   ├── PlayCommandHandlerTests.cs
│   └── ImportSongCommandHandlerTests.cs
└── Queries/
    └── FilterProjectsQueryHandlerTests.cs
```

**Domain/** (test namespace)
```
Domain/
└── ValueObjects/
    ├── KeyTests.cs
    ├── ChordTests.cs
    └── VolumeTests.cs
```

**Total Tests files**: 6 files (minimum; can expand)

---

## MODIFIED PROJECTS (3 total)

### `AiMusicWorkstation.Desktop/` (6 files modified + 4 new)

**Modified files**:
```
✏️  App.xaml.cs                     (add DI container setup - ~80 new lines)
✏️  MainWindow.xaml.cs              (delete business logic, add ViewModel injection - remove ~700 LOC, add ~20 LOC)
✏️  MainWindow.xaml                 (update button bindings to use Commands instead of Click handlers)
✏️  EditSectionWindow.xaml.cs       (minor ViewModel wiring)
✏️  InputWindow.xaml.cs             (minimal changes)
✏️  AiMusicWorkstation.Desktop.csproj (add ProjectReference to Application, Infrastructure; add Logging NuGet)
```

**New files**:
```
✨  ViewModels/ViewModelBase.cs
✨  ViewModels/PlaybackViewModel.cs
✨  ViewModels/LibraryViewModel.cs
✨  ViewModels/AnalysisViewModel.cs
```

**Deleted files**: None (just cleared of business logic)

**Total Desktop changes**: 6 modifications + 4 new ViewModels

---

### `AiMusicWorkstation.Shared/` (1 file modified + 4 new)

**Modified files**:
```
✏️  AiMusicWorkstation.Shared.csproj (no package changes needed)
```

**New files**:
```
✨  Dto/SongProjectDto.cs
✨  Dto/AnalysisResultDto.cs
✨  Dto/ChordDto.cs
✨  Dto/PlaybackStateDto.cs
```

**Unchanged files**:
```
✓  Models/SongProject.cs             (kept as-is)
✓  Helpers/MusicTheoryHelper.cs      (unchanged)
✓  Enums/KeySource.cs                (existing)
```

**Total Shared changes**: 1 modification + 4 new DTOs

---

### `AiMusicWorkstation.Api/` (0-1 file modified)

**No mandatory changes**. Optional:
```
✏️  AiMusicWorkstation.Api.csproj (add ProjectReference to Application if REST API desired)
```

---

## PROJECT FILES (4 new + 3 modified)

**New .csproj files** (created):
```
✨  AiMusicWorkstation.Application/AiMusicWorkstation.Application.csproj
✨  AiMusicWorkstation.Domain/AiMusicWorkstation.Domain.csproj
✨  AiMusicWorkstation.Infrastructure/AiMusicWorkstation.Infrastructure.csproj
✨  AiMusicWorkstation.Tests/AiMusicWorkstation.Tests.csproj
```

**Modified .csproj files**:
```
✏️  AiMusicWorkstation.Desktop.csproj        (add ProjectReference to Application, Infrastructure)
✏️  AiMusicWorkstation.Shared.csproj         (no changes needed)
✏️  AiMusicWorkstation.sln/.slnx             (add 4 new projects)
```

---

## DEPENDENCY CHANGES

**App.xaml.cs Changes** (major DI setup):
```csharp
// ADD (existing + new):
services.AddSingleton<StemPlayer>();
services.AddSingleton<IAudioPlayer>(sp => new AudioPlayerService(...));
services.AddSingleton<IPythonAnalysisService, PythonBridgeService>();
services.AddSingleton<ISmartImporterService, SmartImporterService>();
services.AddSingleton<IMetronome, MetronomeService>();
services.AddSingleton<ILibraryRepository, LibraryRepository>();

// ADD (CQRS):
services.AddSingleton<ICommandBus, CommandBus>();
services.AddSingleton<IQueryBus, QueryBus>();

// ADD (Handlers - 28 total):
services.AddScoped<ICommandHandler<PlayCommand>, PlayCommandHandler>();
services.AddScoped<ICommandHandler<PauseCommand>, PauseCommandHandler>();
// ... (26 more handlers)

// ADD (ViewModels):
services.AddSingleton<PlaybackViewModel>();
services.AddSingleton<LibraryViewModel>();
services.AddSingleton<AnalysisViewModel>();
services.AddSingleton<MainWindowViewModel>();
```

---

## SUMMARY TABLE

| Category | Count | Status |
|----------|-------|--------|
| **New Projects** | 4 | ✨ Created |
| **New Project Files (.csproj)** | 4 | ✨ Created |
| **New Application Files** | 28 | ✨ Created |
| **New Domain Files** | 11 | ✨ Created |
| **New Infrastructure Files** | 6 | ✨ Created |
| **New Test Files** | 6+ | ✨ Created |
| **New ViewModel Files** | 4 | ✨ Created |
| **New DTO Files** | 4 | ✨ Created |
| **Modified Desktop Files** | 6 | ✏️ Modified |
| **Modified Shared Files** | 1 | ✏️ Modified |
| **Modified Project Files (.csproj/.sln)** | 3 | ✏️ Modified |
| **TOTAL NEW FILES** | **59+** | ✨ |
| **TOTAL MODIFIED** | **10** | ✏️ |

---

## LOC (LINES OF CODE) ESTIMATE

| Component | Estimated LOC | Notes |
|-----------|------|-------|
| Commands (15) | 300 | Simple DTOs, ~20 LOC each |
| Queries (13) | 260 | Simple DTOs, ~20 LOC each |
| Command Handlers (15) | 1,500 | Orchestrate services, ~100 LOC each |
| Query Handlers (13) | 800 | Logic + filtering, ~60 LOC each |
| Interfaces (5) | 150 | Service contracts |
| Entities (3) | 150 | Domain entities |
| Value Objects (3) | 200 | Immutable, logic |
| Infrastructure Services (5) | 1,200 | Process mgmt, persistence, adapters |
| ViewModels (4) | 400 | State + command binding |
| DI Setup (App.xaml.cs) | 100 | Registration |
| Unit Tests (20+) | 2,000+ | Handler + domain tests |
| **TOTAL NEW CODE** | **~7,000 LOC** | |
| **DELETED FROM MAINWINDOW** | **~700 LOC** | Moved to handlers/ViewModels |
| **NET NEW** | **~6,300 LOC** | But significantly more testable/maintainable |

---

## FILE CHECKLIST (Use During TASK-001)

### Phase 1: Create Projects
- [ ] AiMusicWorkstation.Application.csproj created
- [ ] AiMusicWorkstation.Domain.csproj created
- [ ] AiMusicWorkstation.Infrastructure.csproj created
- [ ] AiMusicWorkstation.Tests.csproj created
- [ ] Solution file updated with 4 new projects
- [ ] Project references configured correctly

### Phase 2: Domain Layer
- [ ] IAudioPlayer.cs created
- [ ] IPythonAnalysisService.cs created
- [ ] ILibraryRepository.cs created
- [ ] ISmartImporterService.cs created
- [ ] IMetronome.cs created
- [ ] Entities/ folder with 3 files created
- [ ] ValueObjects/ folder with 3 files created

### Phase 3: Infrastructure Layer
- [ ] PythonBridgeService.cs created (with Polly, streaming, process mgmt)
- [ ] AudioPlayerService.cs created (wrapper)
- [ ] SmartImporterService.cs created
- [ ] MetronomeService.cs created
- [ ] LibraryRepository.cs created (async persistence)

### Phase 4: Application Layer
- [ ] Commands/ folder with 15 command files created
- [ ] Queries/ folder with 13 query files created
- [ ] Handlers/Commands/ folder with 15 handler files created
- [ ] Handlers/Queries/ folder with 13 handler files created
- [ ] Bus/ICommandBus.cs and CommandBus.cs created
- [ ] Bus/IQueryBus.cs and QueryBus.cs created
- [ ] Exceptions/ folder with 3 exception files created

### Phase 5: Desktop Layer
- [ ] ViewModels/ folder created with 4 ViewModel files
- [ ] App.xaml.cs updated with DI registration
- [ ] MainWindow.xaml.cs updated (business logic deleted, ViewModel injected)
- [ ] MainWindow.xaml updated (Command bindings)

### Phase 6: Shared Layer
- [ ] Dto/ folder created with 4 DTO files

### Phase 7: Tests
- [ ] Tests project created with xUnit + Moq references
- [ ] Example handler tests created
- [ ] Example domain tests created

### Build & Validation
- [ ] Solution builds with zero errors
- [ ] All namespaces correct
- [ ] All project references valid
- [ ] DI container loads without errors

---

**Use this as a progress tracker during TASK-001 execution!** ✅
