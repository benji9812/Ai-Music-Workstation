using AiMusicWorkstation.Application.Commands.Library;
using AiMusicWorkstation.Domain.Repositories;
using AiMusicWorkstation.Domain.Services;
using AiMusicWorkstation.Domain.Entities;
using Microsoft.Extensions.Logging;

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
