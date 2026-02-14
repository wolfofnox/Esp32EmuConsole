using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Esp32EmuConsole.Tests;

public class ResponseLoggerTest
{
    private readonly Utilities.LogBuffer _logBuffer;
    private readonly Utilities.InMemoryLoggerProvider _provider;
    private readonly ILoggerFactory _loggerFactory;

    public ResponseLoggerTest()
    {
        _logBuffer = new Utilities.LogBuffer();
        _provider = new Utilities.InMemoryLoggerProvider(
            new Utilities.LogRoute("*", LogLevel.Trace, Utilities.LogFormat.Full, _logBuffer)
        );
        _loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(_provider));
    }

    [Fact]
    public async Task InvokeAsync_LogsRequestDetails()
    {
        // Arrange
        var middleware = new Middleware.ResponseLogger(
            next: async (HttpContext ctx) =>
            {
                ctx.Response.StatusCode = 200;
                await Task.CompletedTask;
            },
            logger: _loggerFactory.CreateLogger<Middleware.ResponseLogger>(),
            loggerFactory: _loggerFactory
        );

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/test/path";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var logs = _logBuffer.Snapshot();
        Assert.NotEmpty(logs);
        Assert.Contains(logs, log => log.Contains("HTTP"));
        Assert.Contains(logs, log => log.Contains("GET"));
        Assert.Contains(logs, log => log.Contains("/test/path"));
        Assert.Contains(logs, log => log.Contains("200"));
    }

    [Theory]
    [InlineData("GET", "/api/users", 200)]
    [InlineData("POST", "/api/users", 201)]
    [InlineData("PUT", "/api/users/1", 200)]
    [InlineData("DELETE", "/api/users/1", 204)]
    [InlineData("GET", "/not-found", 404)]
    [InlineData("POST", "/error", 500)]
    public async Task InvokeAsync_LogsDifferentMethodsAndStatusCodes(string method, string path, int statusCode)
    {
        // Arrange
        var middleware = new Middleware.ResponseLogger(
            next: async (HttpContext ctx) =>
            {
                ctx.Response.StatusCode = statusCode;
                await Task.CompletedTask;
            },
            logger: _loggerFactory.CreateLogger<Middleware.ResponseLogger>(),
            loggerFactory: _loggerFactory
        );

        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        _logBuffer.Clear();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var logs = _logBuffer.Snapshot();
        Assert.NotEmpty(logs);
        Assert.Contains(logs, log => log.Contains(method));
        Assert.Contains(logs, log => log.Contains(path));
        Assert.Contains(logs, log => log.Contains(statusCode.ToString()));
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        // Arrange
        bool nextCalled = false;
        var middleware = new Middleware.ResponseLogger(
            next: async (HttpContext ctx) =>
            {
                nextCalled = true;
                ctx.Response.StatusCode = 200;
                await Task.CompletedTask;
            },
            logger: _loggerFactory.CreateLogger<Middleware.ResponseLogger>(),
            loggerFactory: _loggerFactory
        );

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/test";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled, "Next middleware should have been called");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenNextIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Middleware.ResponseLogger(
            next: null!,
            logger: _loggerFactory.CreateLogger<Middleware.ResponseLogger>(),
            loggerFactory: _loggerFactory
        ));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Middleware.ResponseLogger(
            next: (ctx) => Task.CompletedTask,
            logger: null!,
            loggerFactory: _loggerFactory
        ));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerFactoryIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Middleware.ResponseLogger(
            next: (ctx) => Task.CompletedTask,
            logger: _loggerFactory.CreateLogger<Middleware.ResponseLogger>(),
            loggerFactory: null!
        ));
    }
}
