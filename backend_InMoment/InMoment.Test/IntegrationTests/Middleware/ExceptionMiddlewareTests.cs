using System.Text.Json;
using FluentAssertions;
using InMoment.API.Common;
using InMoment.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace InMoment.Tests.IntegrationTests.Middlewar;

public sealed class ExceptionMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionMiddleware>> _logger = new();

    private ExceptionMiddleware Create()
        => new(_logger.Object);

    [Fact]
    public async Task InvokeAsync_ShouldPassThrough_WhenNoException()
    {
        var middleware = Create();
        var context = CreateHttpContext();

        var nextCalled = false;

        Task Next(HttpContext ctx)
        {
            nextCalled = true;
            ctx.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }

        await middleware.InvokeAsync(context, Next);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public async Task InvokeAsync_ShouldMapNotFoundException_To404ProblemDetails()
    {
        var middleware = Create();
        var context = CreateHttpContext();

        Task Next(HttpContext _) => throw new NotFoundException("Group not found.");

        await middleware.InvokeAsync(context, Next);

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        context.Response.ContentType.Should().Be("application/json; charset=utf-8");

        var problem = await ReadProblemDetails(context);
        problem.Title.Should().Be("Group not found.");
        problem.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Extensions.Should().ContainKey("traceId");
        problem.Extensions["traceId"]!.ToString().Should().Be("trace-123");

        _logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Domain exception occurred")),
                It.IsAny<NotFoundException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldMapForbiddenException_To403ProblemDetails()
    {
        var middleware = Create();
        var context = CreateHttpContext();

        Task Next(HttpContext _) => throw new ForbiddenException("Access denied.");

        await middleware.InvokeAsync(context, Next);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        var problem = await ReadProblemDetails(context);
        problem.Title.Should().Be("Access denied.");
        problem.Status.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_ShouldMapValidationException_To400ProblemDetails()
    {
        var middleware = Create();
        var context = CreateHttpContext();

        Task Next(HttpContext _) => throw new ValidationException("Validation failed.");

        await middleware.InvokeAsync(context, Next);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var problem = await ReadProblemDetails(context);
        problem.Title.Should().Be("Validation failed.");
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task InvokeAsync_ShouldMapUnknownException_To500ProblemDetails()
    {
        var middleware = Create();
        var context = CreateHttpContext();

        Task Next(HttpContext _) => throw new InvalidOperationException("boom");

        await middleware.InvokeAsync(context, Next);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Be("application/json; charset=utf-8");

        var problem = await ReadProblemDetails(context);
        problem.Title.Should().Be("Произошла внутренняя ошибка сервера.");
        problem.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problem.Extensions.Should().ContainKey("traceId");
        problem.Extensions["traceId"]!.ToString().Should().Be("trace-123");

        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Unhandled exception occurred")),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldStillWriteProblemDetails_WhenResponseAlreadyStarted()
    {
        var middleware = Create();
        var context = CreateHttpContext();
        await context.Response.StartAsync();

        Task Next(HttpContext _) => throw new ValidationException("Validation failed.");

        await middleware.InvokeAsync(context, Next);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().Be("application/json; charset=utf-8");

        var problem = await ReadProblemDetails(context);
        problem.Title.Should().Be("Validation failed.");
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Extensions.Should().ContainKey("traceId");
        problem.Extensions["traceId"]!.ToString().Should().Be("trace-123");
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "trace-123";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<ProblemDetails> ReadProblemDetails(HttpContext context)
    {
        context.Response.Body.Position = 0;

        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        problem.Should().NotBeNull();
        return problem!;
    }
}