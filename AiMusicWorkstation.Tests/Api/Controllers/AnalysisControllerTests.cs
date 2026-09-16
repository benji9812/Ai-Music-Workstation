using System.Net;
using System.Security.Claims;
using AiMusicWorkstation.Api.Controllers;
using AiMusicWorkstation.Api.Models;
using AiMusicWorkstation.Domain.Entities;
using AiMusicWorkstation.Domain.Repositories;
using AiMusicWorkstation.Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AiMusicWorkstation.Tests.Api.Controllers;

public class AnalysisControllerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GetProjectAnalysis_ProjectWithoutStems_ReturnsSavedProjectWithoutCallingPython()
    {
        var pythonWasCalled = false;
        var project = new SongProject
        {
            Id = "project-1",
            Title = "Original only",
            StemsPath = "",
            OriginalPath = "imports/original.mp3",
            Duration = TimeSpan.FromSeconds(123),
            Lyrics = "[{\"start\":0,\"end\":2,\"text\":\"Hello\"}]",
            Sections = "[{\"label\":\"Intro\",\"start\":0,\"end\":12}]"
        };
        var repository = new Mock<ILibraryRepository>();
        repository
            .Setup(r => r.GetByIdAsync(project.Id, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        var controller = CreateController(_ =>
        {
            pythonWasCalled = true;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }, repository.Object, UserId);

        var response = Assert.IsType<OkObjectResult>(await controller.GetProjectAnalysis(project.Id));
        var dto = Assert.IsType<SavedSongProjectDto>(response.Value);

        Assert.Equal(project.OriginalPath, dto.OriginalPath);
        Assert.Empty(dto.StemsPath);
        Assert.Equal(123, dto.DurationSeconds);
        Assert.Single(dto.Lyrics);
        Assert.Single(dto.Sections);
        Assert.False(pythonWasCalled);
    }

    [Fact]
    public async Task GetProjectAnalysis_MissingProject_ReturnsNotFound()
    {
        var repository = new Mock<ILibraryRepository>();
        repository
            .Setup(r => r.GetByIdAsync("missing", UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongProject?)null);
        var controller = CreateController(_ => AudioResponse(HttpStatusCode.OK, []), repository.Object, UserId);

        Assert.IsType<NotFoundObjectResult>(await controller.GetProjectAnalysis("missing"));
    }

    [Fact]
    public async Task GetProjectAnalysis_UserCannotAccessProject_ReturnsNotFoundAndKeepsUserScope()
    {
        var repository = new Mock<ILibraryRepository>();
        repository
            .Setup(r => r.GetByIdAsync("other-users-project", UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongProject?)null);
        var controller = CreateController(_ => AudioResponse(HttpStatusCode.OK, []), repository.Object, UserId);

        Assert.IsType<NotFoundObjectResult>(await controller.GetProjectAnalysis("other-users-project"));
        repository.Verify(
            r => r.GetByIdAsync("other-users-project", UserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAudio_ForwardsRangeAndPreservesPartialResponse()
    {
        HttpRequestMessage? upstreamRequest = null;
        var controller = CreateController(request =>
        {
            upstreamRequest = request;
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent([2, 3, 4, 5])
            };
            response.Content.Headers.ContentType = new("audio/mpeg");
            response.Content.Headers.ContentLength = 4;
            response.Content.Headers.ContentRange = new(2, 5, 10);
            response.Headers.TryAddWithoutValidation("Accept-Ranges", "bytes");
            response.Headers.TryAddWithoutValidation("ETag", "\"audio-v1\"");
            response.Content.Headers.LastModified = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
            return response;
        });
        controller.Request.Headers.Range = "bytes=2-";
        controller.Request.Headers["If-Range"] = "\"audio-v1\"";

        var result = await controller.GetAudio("track.mp3", Config, CreateFactory(controller));

        Assert.IsType<EmptyResult>(result);
        Assert.NotNull(upstreamRequest);
        Assert.Equal("bytes=2-", upstreamRequest.Headers.Range!.ToString());
        Assert.Equal("\"audio-v1\"", upstreamRequest.Headers.GetValues("If-Range").Single());
        Assert.Equal(StatusCodes.Status206PartialContent, controller.Response.StatusCode);
        Assert.Equal("audio/mpeg", controller.Response.ContentType);
        Assert.Equal(4L, controller.Response.ContentLength.GetValueOrDefault());
        Assert.Equal("bytes 2-5/10", controller.Response.Headers["Content-Range"]);
        Assert.Equal("bytes", controller.Response.Headers["Accept-Ranges"]);
        Assert.Equal("\"audio-v1\"", controller.Response.Headers.ETag);
        Assert.Equal("Thu, 01 Jan 2026 00:00:00 GMT", controller.Response.Headers.LastModified);
        Assert.Equal(new byte[] { 2, 3, 4, 5 }, ReadResponseBody(controller));
    }

    [Fact]
    public async Task GetAudio_WithoutRangePreservesFullResponse()
    {
        var controller = CreateController(_ => AudioResponse(HttpStatusCode.OK, [0, 1, 2, 3]));

        await controller.GetAudio("track.mp3", Config, CreateFactory(controller));

        Assert.Equal(StatusCodes.Status200OK, controller.Response.StatusCode);
        Assert.Equal("audio/mpeg", controller.Response.ContentType);
        Assert.Equal(4L, controller.Response.ContentLength.GetValueOrDefault());
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, ReadResponseBody(controller));
    }

    [Fact]
    public async Task GetAudio_PreservesNotFoundResponse()
    {
        var controller = CreateController(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("missing", System.Text.Encoding.UTF8, "application/json")
        });

        await controller.GetAudio("missing.mp3", Config, CreateFactory(controller));

        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", controller.Response.ContentType);
        Assert.Equal("missing", System.Text.Encoding.UTF8.GetString(ReadResponseBody(controller)));
    }

    [Fact]
    public async Task GetAudio_PreservesRangeNotSatisfiableResponse()
    {
        var controller = CreateController(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                Content = new ByteArrayContent([])
            };
            response.Content.Headers.ContentRange = new(10);
            return response;
        });
        controller.Request.Headers.Range = "bytes=100-";

        var result = await controller.GetAudio("track.mp3", Config, CreateFactory(controller));

        Assert.IsType<EmptyResult>(result);
        Assert.Equal(StatusCodes.Status416RangeNotSatisfiable, controller.Response.StatusCode);
        Assert.Equal("bytes */10", controller.Response.Headers["Content-Range"]);
    }

    private static readonly PythonEngineConfig Config = new() { BaseUrl = "http://python.test/" };

    private static AnalysisController CreateController(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        ILibraryRepository? repository = null,
        Guid? userId = null)
    {
        var handler = new StubHttpMessageHandler(responder);
        var manager = new PythonEngineManager(
            new HttpClient(handler) { BaseAddress = new Uri(Config.BaseUrl) },
            Config,
            NullLogger<PythonEngineManager>.Instance);
        var controller = new AnalysisController(
            new PythonEngineClient(new HttpClient(handler) { BaseAddress = new Uri(Config.BaseUrl) }, manager),
            repository ?? Mock.Of<ILibraryRepository>(),
            NullLogger<AnalysisController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Response.Body = new MemoryStream();
        controller.HttpContext.Items["AudioProxyClient"] = new HttpClient(handler) { BaseAddress = new Uri(Config.BaseUrl) };
        if (userId.HasValue)
        {
            controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())],
                "test"));
        }
        return controller;
    }

    private static HttpResponseMessage AudioResponse(HttpStatusCode statusCode, byte[] bytes)
    {
        var response = new HttpResponseMessage(statusCode) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentType = new("audio/mpeg");
        response.Content.Headers.ContentLength = bytes.Length;
        response.Headers.TryAddWithoutValidation("Accept-Ranges", "bytes");
        return response;
    }

    private static IHttpClientFactory CreateFactory(AnalysisController controller) =>
        new StubHttpClientFactory((HttpClient)controller.HttpContext.Items["AudioProxyClient"]!);

    private static byte[] ReadResponseBody(AnalysisController controller)
    {
        controller.Response.Body.Position = 0;
        return ((MemoryStream)controller.Response.Body).ToArray();
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(responder(request));
    }
}
