using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AiMusicWorkstation.Api.Controllers;
using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Domain.Repositories;
using AiMusicWorkstation.Infrastructure.ExternalServices;
using AiMusicWorkstation.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AiMusicWorkstation.Tests.Api.Controllers;

public class ImportControllerTests
{
    private const string JobId = "job-123";
    private static readonly Guid UserA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task GetImportStatus_RepeatedPolling_PersistsFullImportOnceAndReturnsSameId()
    {
        SongProject? storedProject = null;
        var repository = new Mock<ILibraryRepository>();
        repository
            .Setup(r => r.GetByImportJobIdAsync(JobId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => storedProject);
        repository
            .Setup(r => r.AddAsync(It.IsAny<SongProject>(), It.IsAny<CancellationToken>()))
            .Callback<SongProject, CancellationToken>((project, _) => storedProject = project)
            .Returns(Task.CompletedTask);
        repository
            .Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = CreateController(repository, CompletedImportJson("import", "stems/song"));

        var first = Assert.IsType<ContentResult>(await controller.GetImportStatus(JobId));
        var second = Assert.IsType<ContentResult>(await controller.GetImportStatus(JobId));

        Assert.NotNull(storedProject);
        Assert.Equal(storedProject.Id, ReadResultId(first.Content));
        Assert.Equal(storedProject.Id, ReadResultId(second.Content));
        repository.Verify(r => r.AddAsync(It.IsAny<SongProject>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetImportStatus_ImportJobOwnedByAnotherUser_ReturnsNotFoundWithoutPersisting()
    {
        SongProject? storedProject = null;
        var repository = new Mock<ILibraryRepository>();
        repository
            .Setup(r => r.GetByImportJobIdAsync(JobId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => storedProject);
        repository
            .Setup(r => r.AddAsync(It.IsAny<SongProject>(), It.IsAny<CancellationToken>()))
            .Callback<SongProject, CancellationToken>((project, _) => storedProject = project)
            .Returns(Task.CompletedTask);
        repository
            .Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var ownerController = CreateController(repository, CompletedImportJson("import", "stems/song"), UserA);
        var otherUserController = CreateController(repository, CompletedImportJson("import", "stems/song"), UserB);

        var ownerResponse = Assert.IsType<ContentResult>(await ownerController.GetImportStatus(JobId));
        var otherUserResponse = Assert.IsType<NotFoundResult>(await otherUserController.GetImportStatus(JobId));

        Assert.NotNull(storedProject);
        Assert.Equal(UserA, storedProject.UserId);
        Assert.Equal(storedProject.Id, ReadResultId(ownerResponse.Content));
        Assert.Equal(StatusCodes.Status404NotFound, otherUserResponse.StatusCode);
        repository.Verify(r => r.AddAsync(It.IsAny<SongProject>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetImportStatus_QuickImportWithoutStems_PersistsOnceAndPreservesOriginalPath()
    {
        SongProject? storedProject = null;
        var repository = new Mock<ILibraryRepository>();
        repository
            .Setup(r => r.GetByImportJobIdAsync(JobId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => storedProject);
        repository
            .Setup(r => r.AddAsync(It.IsAny<SongProject>(), It.IsAny<CancellationToken>()))
            .Callback<SongProject, CancellationToken>((project, _) => storedProject = project)
            .Returns(Task.CompletedTask);
        repository
            .Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = CreateController(
            repository,
            CompletedImportJson("quick_import", stemsPath: "", originalPath: "dl_track.mp3"));

        await controller.GetImportStatus(JobId);
        await controller.GetImportStatus(JobId);

        Assert.NotNull(storedProject);
        Assert.Equal("dl_track.mp3", storedProject.OriginalPath);
        Assert.Equal(JobId, storedProject.ImportJobId);
        Assert.Equal(TimeSpan.FromSeconds(180), storedProject.Duration);
        Assert.Equal(4, storedProject.TimeSignature);
        repository.Verify(r => r.AddAsync(It.IsAny<SongProject>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetImportStatus_ConcurrentInsertWonRace_ReturnsExistingProject()
    {
        var winner = new SongProject { Id = "winner-id", ImportJobId = JobId, Title = "Track", UserId = UserA };
        var repository = new Mock<ILibraryRepository>();
        repository
            .SetupSequence(r => r.GetByImportJobIdAsync(JobId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongProject?)null)
            .ReturnsAsync(winner);
        repository
            .Setup(r => r.AddAsync(It.IsAny<SongProject>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository
            .Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unique constraint violation"));

        var controller = CreateController(repository, CompletedImportJson("import", "stems/song"));

        var response = Assert.IsType<ContentResult>(await controller.GetImportStatus(JobId));

        Assert.Equal(winner.Id, ReadResultId(response.Content));
    }

    [Theory]
    [InlineData("lyrics", "{\"lyrics\":[]}")]
    [InlineData("structure", "{\"sections\":[]}")]
    [InlineData("unknown", "{\"title\":\"Track\",\"stems_path\":\"stems/song\"}")]
    public async Task GetImportStatus_NonImportJobs_DoNotTouchLibrary(string jobType, string resultJson)
    {
        var repository = new Mock<ILibraryRepository>();
        var statusJson = $$"""{"job_id":"{{JobId}}","job_type":"{{jobType}}","status":"done","result":{{resultJson}}}""";
        var controller = CreateController(repository, statusJson);

        var response = await controller.GetImportStatus(JobId);

        Assert.IsType<ContentResult>(response);
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("", "stems/song", "")]
    [InlineData("Track", "", "")]
    public async Task GetImportStatus_InvalidImportResult_Returns422WithoutSaving(
        string title,
        string stemsPath,
        string originalPath)
    {
        var repository = new Mock<ILibraryRepository>();
        var controller = CreateController(
            repository,
            CompletedImportJson("import", stemsPath, originalPath, title));

        var response = Assert.IsType<UnprocessableEntityObjectResult>(
            await controller.GetImportStatus(JobId));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, response.StatusCode);
        Assert.Contains("invalid_import_result", JsonSerializer.Serialize(response.Value));
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetImportStatus_PersistenceFailure_ReturnsVisible500Error()
    {
        var repository = new Mock<ILibraryRepository>();
        repository
            .SetupSequence(r => r.GetByImportJobIdAsync(JobId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongProject?)null)
            .ThrowsAsync(new InvalidOperationException("Database unavailable during recovery"));
        repository
            .Setup(r => r.AddAsync(It.IsAny<SongProject>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository
            .Setup(r => r.SaveAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        var controller = CreateController(repository, CompletedImportJson("import", "stems/song"));

        var response = Assert.IsType<ObjectResult>(await controller.GetImportStatus(JobId));
        var body = JsonSerializer.Serialize(response.Value);

        Assert.Equal(StatusCodes.Status500InternalServerError, response.StatusCode);
        Assert.Contains("persistence_failed", body);
        Assert.DoesNotContain("\"status\":\"done\"", body);
    }

    [Fact]
    public async Task SeparateStems_ForwardsJsonPathReferenceToPythonEngine()
    {
        HttpRequestMessage? separationRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/health", StringComparison.Ordinal) == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            separationRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"success\",\"extracted_stems\":{}}", Encoding.UTF8, "application/json")
            };
        });
        var config = new PythonEngineConfig { BaseUrl = "http://python.test/" };
        var manager = new PythonEngineManager(
            new HttpClient(handler) { BaseAddress = new Uri(config.BaseUrl) },
            config,
            NullLogger<PythonEngineManager>.Instance);
        var controller = new ImportController(
            new PythonEngineClient(new HttpClient(handler) { BaseAddress = new Uri(config.BaseUrl) }, manager),
            new Mock<ILibraryRepository>().Object,
            NullLogger<ImportController>.Instance);

        var result = Assert.IsType<ContentResult>(await controller.SeparateStems(new SeparateStemsRequest
        {
            Stems = ["vocals", "drums"],
            FilePath = "track.mp3"
        }));

        Assert.Equal("application/json", separationRequest?.Content?.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await separationRequest!.Content!.ReadAsStringAsync());
        Assert.Equal("track.mp3", document.RootElement.GetProperty("file_path").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("stems").GetArrayLength());
        Assert.Equal("application/json", result.ContentType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SeparateStems_RejectsMissingOriginalFilePath(string? filePath)
    {
        var response = Assert.IsType<BadRequestObjectResult>(await CreateController(
            new Mock<ILibraryRepository>(),
            "{}").SeparateStems(new SeparateStemsRequest { Stems = ["vocals"], FilePath = filePath }));

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SeparateStems_RejectsMissingStems()
    {
        var response = Assert.IsType<BadRequestObjectResult>(await CreateController(
            new Mock<ILibraryRepository>(),
            "{}").SeparateStems(new SeparateStemsRequest { FilePath = "track.mp3", Stems = [] }));

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SeparateStems_RejectsNullRequest()
    {
        var response = Assert.IsType<BadRequestObjectResult>(await CreateController(
            new Mock<ILibraryRepository>(),
            "{}").SeparateStems(null));

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
    }

    private static ImportController CreateController(
        Mock<ILibraryRepository> repository,
        string statusJson,
        Guid? userId = null)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var content = request.RequestUri?.AbsolutePath.EndsWith("/health", StringComparison.Ordinal) == true
                ? "{}"
                : statusJson;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
        });
        var config = new PythonEngineConfig { BaseUrl = "http://python.test/" };
        var managerClient = new HttpClient(handler) { BaseAddress = new Uri(config.BaseUrl) };
        var engineClient = new HttpClient(handler) { BaseAddress = new Uri(config.BaseUrl) };
        var manager = new PythonEngineManager(managerClient, config, NullLogger<PythonEngineManager>.Instance);
        var pythonClient = new PythonEngineClient(engineClient, manager);
        var controller = new ImportController(
            pythonClient,
            repository.Object,
            NullLogger<ImportController>.Instance);

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, (userId ?? UserA).ToString())],
            "test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private static string CompletedImportJson(
        string jobType,
        string stemsPath,
        string originalPath = "",
        string title = "Track")
    {
        return JsonSerializer.Serialize(new
        {
            job_id = JobId,
            job_type = jobType,
            status = "done",
            result = new
            {
                title,
                artist = "Artist",
                bpm = 120.0,
                key = "C",
                stems_path = stemsPath,
                original_path = originalPath,
                duration_seconds = 180.0,
                time_signature = 4
            }
        });
    }

    private static string? ReadResultId(string? content)
    {
        using var document = JsonDocument.Parse(content!);
        return document.RootElement.GetProperty("result").GetProperty("id").GetString();
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
