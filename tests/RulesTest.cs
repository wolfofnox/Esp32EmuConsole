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
                                ""Http"": {
                    ""StatusCode"": 200,
                    ""Body"": ""test response""
                } }
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
        Assert.Equal(200, rules[0].Response?.Http?.StatusCode);

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
        var hasResponse = rulesService.GetHttpResponse("GET", "/any", out _);
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
                                ""Http"": {
                    ""StatusCode"": 200,
                    ""Body"": ""users list""
                } }
            },
            {
                ""Method"": ""POST"",
                ""Uri"": ""/api/users"",
                ""Response"": {
                                ""Http"": {
                    ""StatusCode"": 201,
                    ""Body"": ""user created""
                } }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        var hasGetResponse = rulesService.GetHttpResponse("GET", "/api/users", out var getResp);
        Assert.True(hasGetResponse);
        Assert.Equal(200, getResp?.StatusCode);
        
        var hasPostResponse = rulesService.GetHttpResponse("POST", "/api/users", out var postResp);
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
                                ""Http"": {
                    ""StatusCode"": 200
                } }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        Assert.True(rulesService.GetHttpResponse("GET", "/test", out _));
        Assert.True(rulesService.GetHttpResponse("get", "/test", out _));
        Assert.True(rulesService.GetHttpResponse("Get", "/test", out _));

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
                                ""Http"": {
                    ""StatusCode"": 200
                } }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        Assert.True(rulesService.GetHttpResponse("GET", "/test", out _));

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
        var hasResponse = rulesService.GetHttpResponse("GET", "/test", out var response);
        Assert.True(hasResponse);
        Assert.NotNull(response);
        Assert.Equal(501, response?.StatusCode);

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
                                ""Http"": {
                    ""StatusCode"": 200
                } }
            },
            {
                ""Method"": ""GET"",
                ""Uri"": ""/valid"",
                ""Response"": {
                                ""Http"": {
                    ""StatusCode"": 200
                } }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        var rules = rulesService.GetRules();
        Assert.Equal(2, rules.Count);
        Assert.True(rulesService.GetHttpResponse("GET", "/valid", out _));
        Assert.False(rulesService.GetHttpResponse("GET", "", out _));

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
                                ""Http"": {
                    ""StatusCode"": 200
                } }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        Assert.True(rulesService.GetHttpResponse("GET", "/test", out _));

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
                                ""Http"": {
                    ""StatusCode"": 200
                } }
            },
            {
                ""Method"": ""GET"",
                ""Uri"": ""/test2"",
                ""Response"": {
                                ""Http"": {
                    ""StatusCode"": 200
                } }
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
                                ""Http"": {
                    ""StatusCode"": 200,
                    ""ContentType"": ""application/json"",
                    ""Headers"": {
                        ""X-Custom-Header"": ""CustomValue""
                    },
                    ""Body"": ""{\u0022message\u0022: \u0022hello\u0022}""
                } }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        var hasResponse = rulesService.GetHttpResponse("GET", "/test", out var response);
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
                                ""Http"": {
                    ""StatusCode"": 200,
                    ""Body"": ""response1""
                } }
            },
            {
                ""Method"": ""GET"",
                ""Uri"": ""/test2"",
                ""Response"": {
                                ""Http"": {
                    ""StatusCode"": 201,
                    ""Body"": ""response2""
                } }
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
                        // Read using GetHttpResponse and verify data integrity
                        var found1 = rulesService.GetHttpResponse("GET", "/test1", out var resp1);
                        if (found1)
                        {
                            if (resp1 == null || resp1.StatusCode != 200)
                            {
                                throw new InvalidOperationException("Invalid response data for /test1");
                            }
                        }
                        
                        var found2 = rulesService.GetHttpResponse("GET", "/test2", out var resp2);
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
                                ""Http"": {
                    ""StatusCode"": 200,
                    ""Body"": ""original""
                } }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Verify original rule
        var hasOriginal = rulesService.GetHttpResponse("GET", "/test", out var originalResp);
        Assert.True(hasOriginal);
        Assert.Equal("original", originalResp?.Body);

        // Act - Update rules file and reload
        var updatedRulesJson = @"[
            {
                ""Method"": ""GET"",
                ""Uri"": ""/test"",
                ""Response"": {
                                ""Http"": {
                    ""StatusCode"": 200,
                    ""Body"": ""updated""
                } }
            }
        ]";
        File.WriteAllText(Path.Combine(tempDir, "rules.json"), updatedRulesJson);
        rulesService.ReloadRules();

        // Assert - Rules should be updated
        var hasUpdated = rulesService.GetHttpResponse("GET", "/test", out var updatedResp);
        Assert.True(hasUpdated);
        Assert.Equal("updated", updatedResp?.Body);
    }

    [Fact]
    public void WebSocketRules_AreLoadedCorrectly()
    {
        // Arrange
        var rulesJson = @"[
            {
                ""Uri"": ""/ws"",
                ""Response"": {
                    ""Ws"": {
                        ""Behavior"": ""echo""
                    }
                }
            },
            {
                ""Uri"": ""/ws/sensor"",
                ""Response"": {
                    ""Ws"": {
                        ""Behavior"": ""static"",
                        ""Text"": ""{\u0022temp\u0022:25.5}""
                    }
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        var rules = rulesService.GetRules();
        Assert.Equal(2, rules.Count);
        
        var echoRule = rules[0];
        Assert.Equal("websocket", echoRule.Response?.Ws != null ? "websocket" : "http");
        Assert.Equal("/ws", echoRule.Uri);
        Assert.Equal("echo", echoRule.Response?.Ws?.Behavior);
        
        var staticRule = rules[1];
        Assert.Equal("websocket", staticRule.Response?.Ws != null ? "websocket" : "http");
        Assert.Equal("/ws/sensor", staticRule.Uri);
        Assert.Equal("static", staticRule.Response?.Ws?.Behavior);
        Assert.Contains("temp", staticRule.Response?.Ws?.Text);
    }

    [Fact]
    public void MixedRules_HttpAndWebSocket_AreLoadedCorrectly()
    {
        // Arrange
        var rulesJson = @"[
            {
                ""Method"": ""GET"",
                ""Uri"": ""/api/test"",
                ""Response"": {
                                ""Http"": {
                    ""StatusCode"": 200,
                    ""Body"": ""http response""
                } }
            },
            {
                ""Uri"": ""/ws"",
                ""Response"": {
                    ""Ws"": {
                        ""Behavior"": ""echo""
                    }
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);

        // Act
        using var rulesService = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        // Assert
        var rules = rulesService.GetRules();
        Assert.Equal(2, rules.Count);
        
        // Verify HTTP rule
        var httpRule = rules[0];
        Assert.Equal("GET", httpRule.Method);
        Assert.Equal("/api/test", httpRule.Uri);
        Assert.NotNull(httpRule.Response);
        
        // Verify WebSocket rule
        var wsRule = rules[1];
        Assert.Equal("websocket", wsRule.Response?.Ws != null ? "websocket" : "http");
        Assert.Equal("/ws", wsRule.Uri);
    }
}
