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
using AiMusicWorkstation.Infrastructure.ExternalServices;
using AiMusicWorkstation.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Desktop
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
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

            // Core Services - Existing (wrapped with interfaces)
            services.AddSingleton(new StemPlayer());
            services.AddSingleton<IAudioPlayer>(sp => 
                new AudioPlayerAdapter(sp.GetRequiredService<StemPlayer>()));

            services.AddSingleton(new PythonBridge());
            services.AddSingleton<IPythonAnalysisService>(sp => 
                new PythonAnalysisAdapter(sp.GetRequiredService<PythonBridge>()));

            services.AddSingleton(new LibraryManager());
            // Skip ILibraryRepository for now - will wire separately in handlers

            services.AddSingleton(new SmartImporter(config));
            services.AddSingleton<ISmartImporterService>(sp => 
                new SmartImporterAdapter(sp.GetRequiredService<SmartImporter>()));

            services.AddSingleton(new Metronome());
            services.AddSingleton<IMetronome>(sp => 
                new MetronomeAdapter(sp.GetRequiredService<Metronome>()));

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
}
