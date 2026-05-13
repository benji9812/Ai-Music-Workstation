using System.Windows;
using AiMusicWorkstation.Application.Bus;
using AiMusicWorkstation.Application.Commands.Library;
using AiMusicWorkstation.Application.Commands.Playback;
using AiMusicWorkstation.Application.Handlers.Commands;
using AiMusicWorkstation.Application.Handlers.Queries;
using AiMusicWorkstation.Application.Queries.Library;
using AiMusicWorkstation.Application.Queries.Playback;
using AiMusicWorkstation.Desktop.Services;
using AiMusicWorkstation.Infrastructure.ExternalServices;
using AiMusicWorkstation.Desktop.ViewModels;
using AiMusicWorkstation.Domain.Repositories;
using AiMusicWorkstation.Domain.Services;
using AiMusicWorkstation.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiMusicWorkstation.Desktop
{
    public partial class App : System.Windows.Application
    {
        private IServiceProvider? _serviceProvider;

        protected override async void OnStartup(StartupEventArgs e)
        {
            var services = new ServiceCollection();

            // Logging
            services.AddLogging(config => config.AddDebug().AddConsole());

            // Configuration
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddUserSecrets<App>()
                .Build();
            services.AddSingleton<IConfiguration>(config);

            if (!await EnsureInviteTokenValidatedAsync(config))
            {
                Shutdown();
                return;
            }

            // Core Services
            services.AddSingleton(new StemPlayer());
            services.AddSingleton<IAudioPlayer>(sp =>
                new AudioPlayerAdapter(sp.GetRequiredService<StemPlayer>()));

            services.AddSingleton(sp => PythonConfig.FromConfiguration(sp.GetRequiredService<IConfiguration>()));
            services.AddSingleton<PythonBridge>();
            services.AddSingleton<IPythonAnalysisService>(sp =>
                new PythonAnalysisAdapter(sp.GetRequiredService<PythonBridge>()));

            var libraryManager = new LibraryManager();
            services.AddSingleton(libraryManager);
            services.AddSingleton<ILibraryRepository>(libraryManager);

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
            services.AddSingleton<AnalysisResultParser>();
            services.AddSingleton<AiAnalysisOrchestrator>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();

            // Starta PythonBridge explicit innan MainWindow visas
            _serviceProvider.GetRequiredService<PythonBridge>();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        private static async Task<bool> EnsureInviteTokenValidatedAsync(IConfiguration configuration)
        {
            var inviteTokenStore = new InviteTokenStore();
            if (inviteTokenStore.IsValidated())
            {
                return true;
            }

            var inputWindow = new InputWindow("Enter invite token:");
            bool? accepted = inputWindow.ShowDialog();
            if (accepted != true)
            {
                return false;
            }

            string token = inputWindow.Answer?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("Invite token is required.");
                return false;
            }

            try
            {
                using var client = new InviteTokenClient(configuration);
                bool isValid = await client.ValidateAsync(token);
                if (!isValid)
                {
                    MessageBox.Show("Invalid invite token.");
                    return false;
                }

                inviteTokenStore.MarkValidated(token);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to validate invite token: {ex.Message}");
                return false;
            }
        }
    }
}
