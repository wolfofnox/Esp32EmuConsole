using Microsoft.Extensions.Logging;

namespace Esp32EmuConsole.Tests;

public class RulesTest
{
    private readonly Utilities.LogBuffer _logBuffer;
    private readonly Utilities.InMemoryLoggerProvider _provider;
    private readonly ILoggerFactory _loggerFactory;

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
        File.WriteAllText(Path.Combine(tempDir, "rules.json"), rulesJson);
        return tempDir;
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
        Assert.Single(rulesService.RuleList);
        Assert.Equal("GET", rulesService.RuleList[0].Method);
        Assert.Equal("/test", rulesService.RuleList[0].Uri);
        Assert.NotNull(rulesService.RuleList[0].Response);
        Assert.Equal(200, rulesService.RuleList[0].Response?.StatusCode);

        // Cleanup
        Directory.Delete(tempDir, true);
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
        Assert.Empty(rulesService.RuleList);
        Assert.Empty(rulesService.RuleMap);

        // Cleanup
        Directory.Delete(tempDir, true);
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
        Assert.Empty(rulesService.RuleList);
        var logs = _logBuffer.Snapshot();
        Assert.Contains(logs, log => log.Contains("Error parsing"));

        // Cleanup
        Directory.Delete(tempDir, true);
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
        Assert.True(rulesService.RuleMap.ContainsKey("GET /api/users"));
        Assert.True(rulesService.RuleMap.ContainsKey("POST /api/users"));
        Assert.Equal(200, rulesService.RuleMap["GET /api/users"]?.StatusCode);
        Assert.Equal(201, rulesService.RuleMap["POST /api/users"]?.StatusCode);

        // Cleanup
        Directory.Delete(tempDir, true);
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
        Assert.True(rulesService.RuleMap.ContainsKey("GET /test"));
        Assert.True(rulesService.RuleMap.ContainsKey("get /test"));
        Assert.True(rulesService.RuleMap.ContainsKey("Get /test"));

        // Cleanup
        Directory.Delete(tempDir, true);
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
        Assert.True(rulesService.RuleMap.ContainsKey("GET /test"));

        // Cleanup
        Directory.Delete(tempDir, true);
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
        Assert.Single(rulesService.RuleList);
        Assert.True(rulesService.RuleMap.ContainsKey("GET /test"));
        Assert.Null(rulesService.RuleMap["GET /test"]);

        // Cleanup
        Directory.Delete(tempDir, true);
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
        Assert.Equal(2, rulesService.RuleList.Count);
        Assert.Single(rulesService.RuleMap);
        Assert.True(rulesService.RuleMap.ContainsKey("GET /valid"));

        // Cleanup
        Directory.Delete(tempDir, true);
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
        Assert.True(rulesService.RuleMap.ContainsKey("GET /test"));

        // Cleanup
        Directory.Delete(tempDir, true);
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

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Services.Rules(tempDir, null!));

        // Cleanup
        Directory.Delete(tempDir, true);
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
        Assert.True(rulesService.RuleMap.ContainsKey("GET /test"), "RuleMap should contain 'GET /test'");
        var response = rulesService.RuleMap["GET /test"];
        Assert.NotNull(response);
        Assert.Equal(200, response!.StatusCode);
        Assert.Equal("application/json", response.ContentType);
        Assert.NotNull(response.Headers);
        Assert.Equal("CustomValue", response.Headers["X-Custom-Header"]);
        Assert.Contains("hello", response.Body);

        // Cleanup
        Directory.Delete(tempDir, true);
    }
}
