using AiMusicWorkstation.Application.Commands.Library;
using AiMusicWorkstation.Application.Handlers.Commands;
using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Domain.Repositories;
using AiMusicWorkstation.Domain.Services;
using AiMusicWorkstation.Shared.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AiMusicWorkstation.Tests.Application.Handlers;

public class ImportSongCommandHandlerTests
{
    private readonly Mock<IPythonAnalysisService> _pythonAnalysis;
    private readonly Mock<ISmartImporterService> _importer;
    private readonly Mock<ILibraryRepository> _repository;
    private readonly Mock<IAudioPlayer> _player;
    private readonly Mock<ILogger<ImportSongCommandHandler>> _logger;
    private readonly ImportSongCommandHandler _handler;

    public ImportSongCommandHandlerTests()
    {
        _pythonAnalysis = new Mock<IPythonAnalysisService>();
        _importer = new Mock<ISmartImporterService>();
        _repository = new Mock<ILibraryRepository>();
        _player = new Mock<IAudioPlayer>();
        _logger = new Mock<ILogger<ImportSongCommandHandler>>();

        _handler = new ImportSongCommandHandler(
            _pythonAnalysis.Object,
            _importer.Object,
            _repository.Object,
            _player.Object,
            _logger.Object
        );
    }

    // ── Null-guard tester ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullPythonAnalysis_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ImportSongCommandHandler(
            null!, _importer.Object, _repository.Object, _player.Object, _logger.Object));
    }

    [Fact]
    public void Constructor_NullRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ImportSongCommandHandler(
            _pythonAnalysis.Object, _importer.Object, null!, _player.Object, _logger.Object));
    }

    [Fact]
    public void Constructor_NullImporter_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ImportSongCommandHandler(
            _pythonAnalysis.Object, null!, _repository.Object, _player.Object, _logger.Object));
    }

    // ── Validering av command ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_EmptyFilePath_ThrowsArgumentException()
    {
        var command = new ImportSongCommand { FilePath = "" };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(command));
    }

    [Fact]
    public async Task Handle_FileDoesNotExist_ThrowsFileNotFoundException()
    {
        var command = new ImportSongCommand { FilePath = "C:\\nonexistent\\file.mp3" };

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _handler.Handle(command));
    }

    // ── Happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidFile_CallsAddAsyncOnRepository()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var command = new ImportSongCommand
            {
                FilePath = tempFile,
                CustomTitle = "Test Song",
                ArtistName = "Test Artist"
            };

            _pythonAnalysis
                .Setup(x => x.RunAnalysisAsync(
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AnalysisResult { Bpm = 120f, Key = "C Major", StemsPath = tempFile });

            await _handler.Handle(command);

            _repository.Verify(x => x.AddAsync(
                It.Is<SongProject>(p => p.Title == "Test Song" && p.Artist == "Test Artist"),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public async Task Handle_ValidFile_CallsSaveAsyncOnRepository()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var command = new ImportSongCommand { FilePath = tempFile };

            _pythonAnalysis
                .Setup(x => x.RunAnalysisAsync(
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AnalysisResult { Bpm = 128f, Key = "A Minor", StemsPath = tempFile });

            await _handler.Handle(command);

            _repository.Verify(x => x.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public async Task Handle_NoCustomTitle_UseFileNameAsTitle()
    {
        var tempFile = Path.GetTempFileName();
        string expectedTitle = Path.GetFileNameWithoutExtension(tempFile);
        try
        {
            var command = new ImportSongCommand { FilePath = tempFile };

            _pythonAnalysis
                .Setup(x => x.RunAnalysisAsync(
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AnalysisResult { Bpm = 100f, Key = "G Major", StemsPath = tempFile });

            await _handler.Handle(command);

            _repository.Verify(x => x.AddAsync(
                It.Is<SongProject>(p => p.Title == expectedTitle),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public async Task Handle_NullAnalysisResult_ThrowsInvalidOperationException()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var command = new ImportSongCommand { FilePath = tempFile };

            _pythonAnalysis
                .Setup(x => x.RunAnalysisAsync(
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((AnalysisResult?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _handler.Handle(command));
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public void SongProject_DefaultDateAdded_IsUtc()
    {
        var project = new SongProject();

        Assert.Equal(DateTimeKind.Utc, project.DateAdded.Kind);
    }
}