using System.Text.Json;
using AiMusicWorkstation.Api.Controllers;
using AiMusicWorkstation.Api.Models;
using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AiMusicWorkstation.Tests.Api.Controllers;

public class LibraryControllerTests
{
    [Fact]
    public async Task GetProjects_ReturnsStableCamelCaseContractWithArrayMetadata()
    {
        var project = CreateProject();
        var repository = new Mock<ILibraryRepository>();
        repository
            .Setup(r => r.GetAllAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([project]);
        var controller = CreateController(repository.Object, Mock.Of<ILogger<LibraryController>>());

        var response = Assert.IsType<OkObjectResult>(await controller.GetProjects());
        var dto = Assert.Single(Assert.IsAssignableFrom<IEnumerable<SavedSongProjectDto>>(response.Value));
        var json = JsonSerializer.SerializeToElement(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal("audio/original.mp3", json.GetProperty("originalPath").GetString());
        Assert.Equal("", json.GetProperty("stemsPath").GetString());
        Assert.Equal(95.5, json.GetProperty("durationSeconds").GetDouble());
        Assert.Equal(JsonValueKind.Array, json.GetProperty("lyrics").ValueKind);
        Assert.Equal(JsonValueKind.Array, json.GetProperty("sections").ValueKind);
        Assert.Equal(JsonValueKind.Array, json.GetProperty("chords").ValueKind);
        Assert.Equal("analysis", json.GetProperty("bpmSource").GetString());
        Assert.Equal("analysis", json.GetProperty("keySource").GetString());
        Assert.Equal("analysis", json.GetProperty("timeSignatureSource").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("sectionsSource").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("lyricsSource").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("chordsSource").ValueKind);
        Assert.Equal(project.GroupId.ToString(), json.GetProperty("groupId").GetString());
        var group = json.GetProperty("group");
        Assert.Equal(
            new[] { "createdAt", "id", "name" },
            group.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Equal(project.Group!.Name, group.GetProperty("name").GetString());
        Assert.Equal(
            new[]
            {
                "artist", "bpm", "bpmSource", "chords", "chordsSource", "dateAdded",
                "durationSeconds", "genre", "group", "groupId", "id", "key", "keySource",
                "lyrics", "lyricsSource", "originalPath", "sections", "sectionsSource",
                "stemsPath", "timeSignature", "timeSignatureSource", "title"
            },
            json.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.False(json.TryGetProperty("original_path", out _));
        Assert.False(json.TryGetProperty("extractedStems", out _));
        Assert.False(json.TryGetProperty("userId", out _));
        Assert.False(json.TryGetProperty("importJobId", out _));
    }

    [Fact]
    public async Task GetProjects_MalformedPersistedMetadata_LogsWarningAndReturnsEmptyArray()
    {
        var project = CreateProject();
        project.Lyrics = "not-json";
        project.Sections = "not-json";
        project.Chords = "not-json";
        var repository = new Mock<ILibraryRepository>();
        repository
            .Setup(r => r.GetAllAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([project]);
        var logger = new Mock<ILogger<LibraryController>>();
        var controller = CreateController(repository.Object, logger.Object);

        var response = Assert.IsType<OkObjectResult>(await controller.GetProjects());
        var dto = Assert.Single(Assert.IsAssignableFrom<IEnumerable<SavedSongProjectDto>>(response.Value));

        Assert.Empty(dto.Lyrics);
        Assert.Empty(dto.Sections);
        Assert.Empty(dto.Chords);
        foreach (var fieldName in new[] { "Lyrics", "Sections", "Chords" })
        {
            logger.Verify(
                logger => logger.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) =>
                        state.ToString()!.Contains(project.Id) &&
                        state.ToString()!.Contains(fieldName)),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }

    private static SongProject CreateProject() => new()
    {
        Id = "project-1",
        Title = "Saved song",
        Artist = "Artist",
        Genre = "Rock",
        Bpm = 120,
        Key = "C",
        TimeSignature = 4,
        Duration = TimeSpan.FromSeconds(95.5),
        OriginalPath = "audio/original.mp3",
        StemsPath = "",
        Lyrics = "[{\"start\":0,\"end\":1,\"text\":\"Hi\"}]",
        Sections = "[{\"label\":\"Intro\",\"start\":0,\"end\":10}]",
        Chords = "[{\"time\":0,\"chord\":\"C\"}]",
        BpmSource = DataSource.Analysis,
        KeySource = DataSource.Analysis,
        TimeSigSource = DataSource.Analysis,
        GroupId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Group = new SongGroup
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "Practice",
            CreatedAt = DateTime.Parse("2026-09-16T10:00:00Z").ToUniversalTime()
        }
    };

    private static LibraryController CreateController(
        ILibraryRepository repository,
        ILogger<LibraryController> logger) => new(repository, logger)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
}
