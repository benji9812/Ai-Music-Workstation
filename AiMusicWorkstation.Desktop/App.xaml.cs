using System.Windows;
using AiMusicWorkstation.Application.Bus;
using AiMusicWorkstation.Application.Commands.Library;
using AiMusicWorkstation.Application.Commands.Playback;
using AiMusicWorkstation.Application.Handlers.Commands;
using AiMusicWorkstation.Application.Handlers.Queries;
using AiMusicWorkstation.Application.Queries.Library;
using AiMusicWorkstation.Application.Queries.Playback;
using AiMusicWorkstation.Desktop.Services;
using AiMusicWorkstation.Desktop.ViewModels;
using AiMusicWorkstation.Domain.Repositories;
using AiMusicWorkstation.Domain.Services;
using AiMusicWorkstation.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Desktop
{
    public partial class App : System.Windows.Application
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

            // Core Services
            services.AddSingleton(new StemPlayer());
            services.AddSingleton<IAudioPlayer>(sp =>
                new AudioPlayerAdapter(sp.GetRequiredService<StemPlayer>()));

            services.AddSingleton(new PythonBridge());
            services.AddSingleton<IPythonAnalysisService>(sp =>
                new PythonAnalysisAdapter(sp.GetRequiredService<PythonBridge>()));

            var libraryManager = new LibraryManager();
            services.AddSingleton(libraryManager);
            services.AddSingleton<ILibraryRepository, LibraryRepository>();

            services.AddSingleton(new SmartImporter(config));
            services.AddSingleton<ISmartImporterService>(sp =>
                new SmartImporterAdapter(sp.GetRequiredService<SmartImporter>()));

            services.AddSingleton(new Metronome());
            services.AddSingleton<IMetronome>(sp =>
                new MetronomeAdapter(sp.GetRequiredService<Metronome>()));

            // CQRS Bus
            services.AddSingleton<ICommandBus, CommandBus>();
            services.AddSingleton<IQueryBus, QueryBus>();

            // Command Handlers
            services.AddScoped<ICommandHandler<PlayCommand>, PlayCommandHandler>();
            services.AddScoped<ICommandHandler<PauseCommand>, PauseCommandHandler>();
            services.AddScoped<ICommandHandler<StopCommand>, StopCommandHandler>();
            services.AddScoped<ICommandHandler<DeleteProjectCommand>, DeleteProjectCommandHandler>();
            services.AddScoped<ICommandHandler<ImportSongCommand>, ImportSongCommandHandler>();

            // Query Handlers
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
}