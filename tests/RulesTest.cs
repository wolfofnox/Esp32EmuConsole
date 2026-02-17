using Microsoft.Extensions.Logging;

namespace Esp32EmuConsole.Tests;

public class RulesTest : IDisposable
{
    private readonly Utilities.LogBuffer _logBuffer;
    private readonly Utilities.InMemoryLoggerProvider _provider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<string> _tempDirectories = new();

    public RulesTest()
    {
        _logBuffer = new Utilities.LogBuffer();
        _provider = new Utilities.InMemoryLoggerProvider(
            new Utilities.LogRoute("*", LogLevel.Trace, Utilities.LogFormat.Full, _logBuffer)
        );
        _loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(_provider));
    }

    private string CreateTempDirectoryWithRulesFile(string rulesJson)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "rules.json"), rulesJson);
        return tempDir;
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
    public void Constructor_LoadsRulesFromFile()
    {
        // Arrange
        var rulesJson = @"[
            {
                ""Method"": ""GET"",
                ""Uri"": ""/test"",
                ""Response"": {
                    ""StatusCode"": 200,
                    ""Body"": ""test response""
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        var rules = rulesService.GetRules();
        Assert.Single(rules);
        Assert.Equal("GET", rules[0].Method);
        Assert.Equal("/test", rules[0].Uri);
        Assert.NotNull(rules[0].Response);
        Assert.Equal(200, rules[0].Response?.StatusCode);

    }

    [Fact]
    public void Constructor_CreatesEmptyRulesWhenFileDoesNotExist()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        Assert.Empty(rulesService.GetRules());
        var hasResponse = rulesService.TryGetResponse("GET", "/any", out _);
        Assert.False(hasResponse);

    }

    [Fact]
    public void Constructor_HandlesInvalidJsonGracefully()
    {
        // Arrange
        var invalidJson = "{ invalid json }";
        var tempDir = CreateTempDirectoryWithRulesFile(invalidJson);

        _logBuffer.Clear();

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        Assert.Empty(rulesService.GetRules());
        var logs = _logBuffer.Snapshot();
        Assert.Contains(logs, log => log.Contains("Error parsing"));

    }

    [Fact]
    public void RuleMap_MatchesMethodAndUri()
    {
        // Arrange
        var rulesJson = @"[
            {
                ""Method"": ""GET"",
                ""Uri"": ""/api/users"",
                ""Response"": {
                    ""StatusCode"": 200,
                    ""Body"": ""users list""
                }
            },
            {
                ""Method"": ""POST"",
                ""Uri"": ""/api/users"",
                ""Response"": {
                    ""StatusCode"": 201,
                    ""Body"": ""user created""
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        var hasGetResponse = rulesService.TryGetResponse("GET", "/api/users", out var getResp);
        Assert.True(hasGetResponse);
        Assert.Equal(200, getResp?.StatusCode);
        
        var hasPostResponse = rulesService.TryGetResponse("POST", "/api/users", out var postResp);
        Assert.True(hasPostResponse);
        Assert.Equal(201, postResp?.StatusCode);

    }

    [Fact]
    public void RuleMap_IsCaseInsensitive()
    {
        // Arrange
        var rulesJson = @"[
            {
                ""Method"": ""get"",
                ""Uri"": ""/test"",
                ""Response"": {
                    ""StatusCode"": 200
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        Assert.True(rulesService.TryGetResponse("GET", "/test", out _));
        Assert.True(rulesService.TryGetResponse("get", "/test", out _));
        Assert.True(rulesService.TryGetResponse("Get", "/test", out _));

    }

    [Fact]
    public void RuleMap_NormalizesPathWithLeadingSlash()
    {
        // Arrange
        var rulesJson = @"[
            {
                ""Method"": ""GET"",
                ""Uri"": ""test"",
                ""Response"": {
                    ""StatusCode"": 200
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        Assert.True(rulesService.TryGetResponse("GET", "/test", out _));

    }

    [Fact]
    public void RuleMap_AllowsNullResponse()
    {
        // Arrange
        var rulesJson = @"[
            {
                ""Method"": ""GET"",
                ""Uri"": ""/test"",
                ""Response"": null
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        var rules = rulesService.GetRules();
        Assert.Single(rules);
        var hasResponse = rulesService.TryGetResponse("GET", "/test", out var response);
        Assert.True(hasResponse);
        Assert.Null(response);

    }

    [Fact]
    public void RuleMap_SkipsRulesWithEmptyUri()
    {
        // Arrange
        var rulesJson = @"[
            {
                ""Method"": ""GET"",
                ""Uri"": """",
                ""Response"": {
                    ""StatusCode"": 200
                }
            },
            {
                ""Method"": ""GET"",
                ""Uri"": ""/valid"",
                ""Response"": {
                    ""StatusCode"": 200
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        var rules = rulesService.GetRules();
        Assert.Equal(2, rules.Count);
        Assert.True(rulesService.TryGetResponse("GET", "/valid", out _));
        Assert.False(rulesService.TryGetResponse("GET", "", out _));

    }

    [Fact]
    public void RuleMap_DefaultsToGetMethod()
    {
        // Arrange
        var rulesJson = @"[
            {
                ""Method"": """",
                ""Uri"": ""/test"",
                ""Response"": {
                    ""StatusCode"": 200
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        Assert.True(rulesService.TryGetResponse("GET", "/test", out _));

    }

    [Fact]
    public void Constructor_LogsNumberOfLoadedRules()
    {
        // Arrange
        var rulesJson = @"[
            {
                ""Method"": ""GET"",
                ""Uri"": ""/test1"",
                ""Response"": {
                    ""StatusCode"": 200
                }
            },
            {
                ""Method"": ""GET"",
                ""Uri"": ""/test2"",
                ""Response"": {
                    ""StatusCode"": 200
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);

        _logBuffer.Clear();

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        var logs = _logBuffer.Snapshot();
        Assert.Contains(logs, log => log.Contains("Loaded"));
        Assert.Contains(logs, log => log.Contains("2"));
        Assert.Contains(logs, log => log.Contains("rules"));

    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Services.Rules(tempDir, null!));

    }

    [Fact]
    public void RuleMap_HandlesComplexResponse()
    {
        // Arrange
        var rulesJson = @"[
            {
                ""Method"": ""GET"",
                ""Uri"": ""/test"",
                ""Response"": {
                    ""StatusCode"": 200,
                    ""ContentType"": ""application/json"",
                    ""Headers"": {
                        ""X-Custom-Header"": ""CustomValue""
                    },
                    ""Body"": ""{\u0022message\u0022: \u0022hello\u0022}""
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        var hasResponse = rulesService.TryGetResponse("GET", "/test", out var response);
        Assert.True(hasResponse, "Should find response for 'GET /test'");
        Assert.NotNull(response);
        Assert.Equal(200, response!.StatusCode);
        Assert.Equal("application/json", response.ContentType);
        Assert.NotNull(response.Headers);
        Assert.Equal("CustomValue", response.Headers["X-Custom-Header"]);
        Assert.Contains("hello", response.Body);

    }

    [Fact]
    public void ConcurrentAccess_IsThreadSafe()
    {
        // Arrange
        var rulesJson = @"[
            {
                ""Method"": ""GET"",
                ""Uri"": ""/test1"",
                ""Response"": {
                    ""StatusCode"": 200,
                    ""Body"": ""response1""
                }
            },
            {
                ""Method"": ""GET"",
                ""Uri"": ""/test2"",
                ""Response"": {
                    ""StatusCode"": 201,
                    ""Body"": ""response2""
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Act - Run multiple threads reading and one thread reloading
        var readThreads = new List<Thread>();
        var exceptions = new List<Exception>();
        var shouldStop = false;

        // Create multiple reader threads
        for (int i = 0; i < 5; i++)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    while (!shouldStop)
                    {
                        // Read using TryGetResponse and verify data integrity
                        var found1 = rulesService.TryGetResponse("GET", "/test1", out var resp1);
                        if (found1)
                        {
                            if (resp1 == null || resp1.StatusCode != 200)
                            {
                                throw new InvalidOperationException("Invalid response data for /test1");
                            }
                        }
                        
                        var found2 = rulesService.TryGetResponse("GET", "/test2", out var resp2);
                        if (found2)
                        {
                            if (resp2 == null || resp2.StatusCode != 201)
                            {
                                throw new InvalidOperationException("Invalid response data for /test2");
                            }
                        }
                        
                        // Read using GetRules and verify list integrity
                        var rules = rulesService.GetRules();
                        if (rules.Count != 2)
                        {
                            throw new InvalidOperationException($"Expected 2 rules but got {rules.Count}");
                        }
                        
                        // Small delay to allow other threads to run
                        Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            });
            thread.Start();
            readThreads.Add(thread);
        }

        // Create a thread that repeatedly reloads rules
        var reloadThread = new Thread(() =>
        {
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    rulesService.ReloadRules();
                    Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                lock (exceptions)
                {
                    exceptions.Add(ex);
                }
            }
        });
        reloadThread.Start();

        // Wait for reload thread to complete
        reloadThread.Join();
        
        // Signal readers to stop
        shouldStop = true;
        
        // Wait for all reader threads to complete
        foreach (var thread in readThreads)
        {
            thread.Join();
        }

        // Assert - No exceptions should have occurred
        Assert.Empty(exceptions);
    }

    [Fact]
    public void ReloadRules_UpdatesRulesSuccessfully()
    {
        // Arrange
        var rulesJson = @"[
            {
                ""Method"": ""GET"",
                ""Uri"": ""/test"",
                ""Response"": {
                    ""StatusCode"": 200,
                    ""Body"": ""original""
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Verify original rule
        var hasOriginal = rulesService.TryGetResponse("GET", "/test", out var originalResp);
        Assert.True(hasOriginal);
        Assert.Equal("original", originalResp?.Body);

        // Act - Update rules file and reload
        var updatedRulesJson = @"[
            {
                ""Method"": ""GET"",
                ""Uri"": ""/test"",
                ""Response"": {
                    ""StatusCode"": 200,
                    ""Body"": ""updated""
                }
            }
        ]";
        File.WriteAllText(Path.Combine(tempDir, "rules.json"), updatedRulesJson);
        rulesService.ReloadRules();

        // Assert - Rules should be updated
        var hasUpdated = rulesService.TryGetResponse("GET", "/test", out var updatedResp);
        Assert.True(hasUpdated);
        Assert.Equal("updated", updatedResp?.Body);
    }
}
