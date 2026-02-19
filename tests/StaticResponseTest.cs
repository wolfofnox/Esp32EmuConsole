using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Esp32EmuConsole.Tests;

public class StaticResponseTest : IDisposable
{
    private readonly Utilities.LogBuffer _logBuffer;
    private readonly Utilities.InMemoryLoggerProvider _provider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<string> _tempDirectories = new();

    public StaticResponseTest()
    {
        _logBuffer = new Utilities.LogBuffer();
        _provider = new Utilities.InMemoryLoggerProvider(
            new Utilities.LogRoute("*", LogLevel.Trace, Utilities.LogFormat.Full, _logBuffer)
        );
        _loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(_provider));
    }

    private Services.Rules CreateRulesService(List<Rule>? rules = null)
    {
        // Create a temporary directory for testing
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);

        // Create a rules.json file if rules are provided
        if (rules != null && rules.Count > 0)
        {
            var rulesJson = System.Text.Json.JsonSerializer.Serialize(rules);
            File.WriteAllText(Path.Combine(tempDir, "rules.json"), rulesJson);
        }

        var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());
        return rulesService;
    }

    public void Dispose()
    {
        // Clean up all temporary directories
        foreach (var tempDir in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task InvokeAsync_ReturnsFixedResponse_WhenRuleMatches()
    {
        // Arrange
        var rules = new List<Rule>
        {
            new Rule
            {
                Method = "GET",
                Uri = "/test",
                Response = new RuleResponse { Http = new HttpResponse
                {
                    StatusCode = 200,
                    ContentType = "application/json",
                    Body = "{\"message\":\"test response\"}"
                } }
            }
        };

        var rulesService = CreateRulesService(rules);
        var middleware = new Middleware.StaticResponse(
            next: (ctx) => Task.CompletedTask,
            ruleService: rulesService,
            logger: _loggerFactory.CreateLogger<Middleware.StaticResponse>()
        );

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/test";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Equal("{\"message\":\"test response\"}", body);

    }

    [Fact]
    public async Task InvokeAsync_Returns501_WhenRuleMatchesButNoResponseDefined()
    {
        // Arrange
        var rules = new List<Rule>
        {
            new Rule
            {
                Method = "GET",
                Uri = "/test",
                Response = null
            }
        };

        var rulesService = CreateRulesService(rules);
        var middleware = new Middleware.StaticResponse(
            next: (ctx) => Task.CompletedTask,
            ruleService: rulesService,
            logger: _loggerFactory.CreateLogger<Middleware.StaticResponse>()
        );

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/test";
        context.Response.Body = new MemoryStream();

        _logBuffer.Clear();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(501, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Contains("No response defined", body);

        var logs = _logBuffer.Snapshot();
        Assert.Contains(logs, log => log.Contains("No response defined"));

    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware_WhenNoRuleMatches()
    {
        // Arrange
        var rules = new List<Rule>
        {
            new Rule
            {
                Method = "GET",
                Uri = "/test",
                Response = new RuleResponse { Http = new HttpResponse { StatusCode = 200, Body = "test" } }
            }
        };

        bool nextCalled = false;
        var rulesService = CreateRulesService(rules);
        var middleware = new Middleware.StaticResponse(
            next: (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            ruleService: rulesService,
            logger: _loggerFactory.CreateLogger<Middleware.StaticResponse>()
        );

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/other-path";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled, "Next middleware should have been called when no rule matches");

    }

    [Theory]
    [InlineData("GET", "/api/users")]
    [InlineData("POST", "/api/users")]
    [InlineData("PUT", "/api/users/1")]
    [InlineData("DELETE", "/api/users/1")]
    public async Task InvokeAsync_MatchesCorrectMethodAndPath(string method, string path)
    {
        // Arrange
        var rules = new List<Rule>
        {
            new Rule
            {
                Method = method,
                Uri = path,
                Response = new RuleResponse { Http = new HttpResponse
                {
                    StatusCode = 200,
                    Body = $"Response for {method} {path}"
                } }
            }
        };

        var rulesService = CreateRulesService(rules);
        var middleware = new Middleware.StaticResponse(
            next: (ctx) => Task.CompletedTask,
            ruleService: rulesService,
            logger: _loggerFactory.CreateLogger<Middleware.StaticResponse>()
        );

        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(200, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Equal($"Response for {method} {path}", body);

    }

    [Fact]
    public async Task InvokeAsync_AppliesCustomHeaders()
    {
        // Arrange
        var rules = new List<Rule>
        {
            new Rule
            {
                Method = "GET",
                Uri = "/test",
                Response = new RuleResponse { Http = new HttpResponse
                {
                    StatusCode = 200,
                    Headers = new Dictionary<string, string>
                    {
                        { "X-Custom-Header", "CustomValue" },
                        { "X-Another-Header", "AnotherValue" }
                    },
                    Body = "test"
                } }
            }
        };

        var rulesService = CreateRulesService(rules);
        var middleware = new Middleware.StaticResponse(
            next: (ctx) => Task.CompletedTask,
            ruleService: rulesService,
            logger: _loggerFactory.CreateLogger<Middleware.StaticResponse>()
        );

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/test";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("CustomValue", context.Response.Headers["X-Custom-Header"].ToString());
        Assert.Equal("AnotherValue", context.Response.Headers["X-Another-Header"].ToString());

    }

    [Fact]
    public async Task InvokeAsync_SetsContentTypeFromResponseProperty()
    {
        // Arrange
        var rules = new List<Rule>
        {
            new Rule
            {
                Method = "GET",
                Uri = "/test",
                Response = new RuleResponse { Http = new HttpResponse
                {
                    StatusCode = 200,
                    ContentType = "text/plain",
                    Body = "plain text"
                } }
            }
        };

        var rulesService = CreateRulesService(rules);
        var middleware = new Middleware.StaticResponse(
            next: (ctx) => Task.CompletedTask,
            ruleService: rulesService,
            logger: _loggerFactory.CreateLogger<Middleware.StaticResponse>()
        );

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/test";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("text/plain", context.Response.ContentType);

    }

    [Fact]
    public async Task InvokeAsync_PrefersContentTypeFromHeaders()
    {
        // Arrange
        var rules = new List<Rule>
        {
            new Rule
            {
                Method = "GET",
                Uri = "/test",
                Response = new RuleResponse { Http = new HttpResponse
                {
                    StatusCode = 200,
                    ContentType = "text/plain",
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/json" }
                    },
                    Body = "{}"
                } }
            }
        };

        var rulesService = CreateRulesService(rules);
        var middleware = new Middleware.StaticResponse(
            next: (ctx) => Task.CompletedTask,
            ruleService: rulesService,
            logger: _loggerFactory.CreateLogger<Middleware.StaticResponse>()
        );

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/test";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("application/json", context.Response.ContentType);

    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenNextIsNull()
    {
        // Arrange
        var rulesService = CreateRulesService();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Middleware.StaticResponse(
            next: null!,
            ruleService: rulesService,
            logger: _loggerFactory.CreateLogger<Middleware.StaticResponse>()
        ));

    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenRuleServiceIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Middleware.StaticResponse(
            next: (ctx) => Task.CompletedTask,
            ruleService: null!,
            logger: _loggerFactory.CreateLogger<Middleware.StaticResponse>()
        ));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        // Arrange
        var rulesService = CreateRulesService();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Middleware.StaticResponse(
            next: (ctx) => Task.CompletedTask,
            ruleService: rulesService,
            logger: null!
        ));

    }
}
