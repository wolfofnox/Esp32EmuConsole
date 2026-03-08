using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Services = Esp32EmuConsole.Services;

namespace Esp32EmuConsole.Tests;

public class WebSocketServiceTest : IDisposable
{
    private const int TestTimeoutMs = 100;
    
    private readonly Utilities.LogBuffer _logBuffer;
    private readonly Utilities.InMemoryLoggerProvider _provider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<string> _tempDirectories = new();

    public WebSocketServiceTest()
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
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        var tempDir = CreateTempDirectoryWithRulesFile("[]");
        using var rules = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());

        Assert.Throws<ArgumentNullException>(() => new Services.WebSocket.WebSocketService(null!, _loggerFactory, rules));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenRulesIsNull()
    {
        var logger = _loggerFactory.CreateLogger<Services.WebSocket.WebSocketService>();

        Assert.Throws<ArgumentNullException>(() => new Services.WebSocket.WebSocketService(logger, _loggerFactory, null!));
    }

    [Fact]
    public async Task HandleConnectionAsync_LogsConnectionEstablished()
    {
        // Arrange
        var rulesJson = @"[]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);
        using var rules = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());
        var wsService = new Services.WebSocket.WebSocketService(_loggerFactory.CreateLogger<Services.WebSocket.WebSocketService>(), _loggerFactory, rules);

        var mockWebSocket = new MockWebSocket();
        _logBuffer.Clear();

        // Act
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TestTimeoutMs);

        try
        {
            await wsService.HandleConnectionAsync(mockWebSocket, "/ws", cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        var logs = _logBuffer.Snapshot();
        Assert.Contains(logs, log => log.Contains("Connection opened") && log.Contains("/ws"));
    }

    [Fact]
    public async Task HandleConnectionAsync_SendsHelloMessage()
    {
        // Arrange
        var rulesJson = @"[]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);
        using var rules = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());
        var wsService = new Services.WebSocket.WebSocketService(_loggerFactory.CreateLogger<Services.WebSocket.WebSocketService>(), _loggerFactory, rules);

        var mockWebSocket = new MockWebSocket();

        // Act
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TestTimeoutMs);

        try
        {
            await wsService.HandleConnectionAsync(mockWebSocket, "/ws", cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        Assert.NotEmpty(mockWebSocket.SentMessages);
        var helloMessage = mockWebSocket.SentMessages[0];
        Assert.Contains("hello", helloMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Connected", helloMessage);
    }

    [Fact]
    public async Task HandleConnectionAsync_EchoBehavior_ReturnsReceivedMessage()
    {
        // Arrange
        var rulesJson = @"[
            {
                ""uri"": ""/ws"",
                ""response"": {
                    ""ws"": [
                        { ""behavior"": ""echo"" }
                    ]
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);
        using var rules = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());
        var wsService = new Services.WebSocket.WebSocketService(_loggerFactory.CreateLogger<Services.WebSocket.WebSocketService>(), _loggerFactory, rules);

        var mockWebSocket = new MockWebSocket();
        mockWebSocket.ReceivedMessages.Add("test message");

        // Act
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TestTimeoutMs);

        try
        {
            await wsService.HandleConnectionAsync(mockWebSocket, "/ws", cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Should have hello message + echo response
        Assert.True(mockWebSocket.SentMessages.Count >= 2);
        var echoMessage = mockWebSocket.SentMessages[1];
        Assert.Equal("test message", echoMessage);
    }

    [Fact]
    public async Task HandleConnectionAsync_StaticBehavior_ReturnsStaticResponse()
    {
        // Arrange
        var rulesJson = @"[
            {
                ""uri"": ""/ws/sensor"",
                ""response"": {
                    ""ws"": [
                        { ""behavior"": ""static"", ""text"": ""{\u0022temp\u0022:25.5}"" }
                    ]
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);
        using var rules = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());
        var wsService = new Services.WebSocket.WebSocketService(_loggerFactory.CreateLogger<Services.WebSocket.WebSocketService>(), _loggerFactory, rules);

        var mockWebSocket = new MockWebSocket();
        mockWebSocket.ReceivedMessages.Add("any message");

        // Act
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TestTimeoutMs);

        try
        {
            await wsService.HandleConnectionAsync(mockWebSocket, "/ws/sensor", cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Should have hello message + static response
        Assert.True(mockWebSocket.SentMessages.Count >= 2);
        var staticResponse = mockWebSocket.SentMessages[1];
        Assert.Contains("temp", staticResponse);
        Assert.Contains("25.5", staticResponse);
    }

    [Fact]
    public async Task HandleConnectionAsync_NoMatchingRule_DoesNotSendResponse()
    {
        // Arrange
        var rulesJson = @"[]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);
        using var rules = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());
        var wsService = new Services.WebSocket.WebSocketService(_loggerFactory.CreateLogger<Services.WebSocket.WebSocketService>(), _loggerFactory, rules);

        var mockWebSocket = new MockWebSocket();
        mockWebSocket.ReceivedMessages.Add("test message");

        // Act
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TestTimeoutMs);

        try
        {
            await wsService.HandleConnectionAsync(mockWebSocket, "/ws", cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Should only have hello message, no response to received message
        Assert.Single(mockWebSocket.SentMessages);
        Assert.Contains("hello", mockWebSocket.SentMessages[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleConnectionAsync_IntervalBehavior_SendsPeriodicMessages()
    {
        // Arrange
        var rulesJson = @"[
            {
                ""uri"": ""/ws/interval"",
                ""response"": {
                    ""ws"": [
                        { ""behavior"": ""interval"", ""intervalMs"": 50, ""text"": ""periodic message"" }
                    ]
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);
        using var rules = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());
        var wsService = new Services.WebSocket.WebSocketService(_loggerFactory.CreateLogger<Services.WebSocket.WebSocketService>(), _loggerFactory, rules);

        var mockWebSocket = new MockWebSocket();

        // Act
        var cts = new CancellationTokenSource();
        cts.CancelAfter(200); // Wait 200ms to get multiple interval messages

        try
        {
            await wsService.HandleConnectionAsync(mockWebSocket, "/ws/interval", cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Should have hello message + at least 1 periodic message
        Assert.True(mockWebSocket.SentMessages.Count >= 2, $"Expected at least 2 messages (hello + 1 periodic), got {mockWebSocket.SentMessages.Count}");
        Assert.Contains("hello", mockWebSocket.SentMessages[0], StringComparison.OrdinalIgnoreCase);
        // Verify at least one periodic message was sent
        Assert.Contains(mockWebSocket.SentMessages.Skip(1), msg => msg.Contains("periodic message"));
    }

    [Fact]
    public async Task HandleConnectionAsync_BinaryBehavior_SendsBinaryData()
    {
        // Arrange - "48656C6C6F" is hex for "Hello"
        var rulesJson = @"[
            {
                ""uri"": ""/ws/binary"",
                ""response"": {
                    ""ws"": [
                        { ""behavior"": ""static"", ""binary"": ""48656C6C6F"" }
                    ]
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);
        using var rules = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());
        var wsService = new Services.WebSocket.WebSocketService(_loggerFactory.CreateLogger<Services.WebSocket.WebSocketService>(), _loggerFactory, rules);

        var mockWebSocket = new MockWebSocket();
        mockWebSocket.ReceivedMessages.Add("trigger");

        // Act
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TestTimeoutMs);

        try
        {
            await wsService.HandleConnectionAsync(mockWebSocket, "/ws/binary", cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Should have hello message + binary response
        Assert.True(mockWebSocket.SentMessages.Count >= 2);
        // The binary data should be sent (MockWebSocket converts it to string, so we check the bytes)
        var binaryResponse = mockWebSocket.SentMessages[1];
        Assert.Equal("Hello", binaryResponse);
    }

    [Fact]
    public async Task HandleConnectionAsync_MatchPattern_OnlyRespondsToMatchingMessages()
    {
        // Arrange – rule only applies to messages matching "^ping$"
        var rulesJson = @"[
            {
                ""uri"": ""/ws"",
                ""response"": {
                    ""ws"": [
                        { ""behavior"": ""static"", ""text"": ""pong"", ""match"": ""^ping$"" }
                    ]
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);
        using var rules = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());
        var wsService = new Services.WebSocket.WebSocketService(_loggerFactory.CreateLogger<Services.WebSocket.WebSocketService>(), _loggerFactory, rules);

        var mockWebSocket = new MockWebSocket();
        // First message does NOT match; second message DOES match
        mockWebSocket.ReceivedMessages.Add("hello");
        mockWebSocket.ReceivedMessages.Add("ping");

        // Act
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TestTimeoutMs);

        try
        {
            await wsService.HandleConnectionAsync(mockWebSocket, "/ws", cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert – hello message + "pong" reply to "ping" (non-matching "hello" yields no reply)
        Assert.Equal(2, mockWebSocket.SentMessages.Count);
        Assert.Contains("hello", mockWebSocket.SentMessages[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains(mockWebSocket.SentMessages, msg => msg == "pong");
    }

    [Fact]
    public async Task HandleConnectionAsync_MultipleIntervalBehaviors_SendsAllPeriodicMessages()
    {
        // Arrange – two interval rules on the same path
        var rulesJson = @"[
            {
                ""uri"": ""/ws/multi"",
                ""response"": {
                    ""ws"": [
                        { ""behavior"": ""interval"", ""intervalMs"": 40, ""text"": ""tick-A"" },
                        { ""behavior"": ""interval"", ""intervalMs"": 60, ""text"": ""tick-B"" }
                    ]
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);
        using var rules = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());
        var wsService = new Services.WebSocket.WebSocketService(_loggerFactory.CreateLogger<Services.WebSocket.WebSocketService>(), _loggerFactory, rules);

        var mockWebSocket = new MockWebSocket();

        // Act – run for 200 ms; both interval tasks should fire at least once
        var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        try
        {
            await wsService.HandleConnectionAsync(mockWebSocket, "/ws/multi", cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert – hello + at least one tick-A and one tick-B
        Assert.True(mockWebSocket.SentMessages.Count >= 3, $"Expected at least 3 messages, got {mockWebSocket.SentMessages.Count}");
        Assert.Contains(mockWebSocket.SentMessages, msg => msg.Contains("tick-A"));
        Assert.Contains(mockWebSocket.SentMessages, msg => msg.Contains("tick-B"));
    }

    [Fact]
    public async Task HandleConnectionAsync_MultipleStaticBehaviors_SendsAllStaticMessages()
    {
        // Arrange – two non-interval (static) rules on the same path
        var rulesJson = @"[
            {
                ""uri"": ""/ws/static-multi"",
                ""response"": {
                    ""ws"": [
                        { ""behavior"": ""static"", ""text"": ""static-A"" },
                        { ""behavior"": ""static"", ""text"": ""static-B"" }
                    ]
                }
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);
        using var rules = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());
        var wsService = new Services.WebSocket.WebSocketService(_loggerFactory.CreateLogger<Services.WebSocket.WebSocketService>(), _loggerFactory, rules);

        var mockWebSocket = new MockWebSocket();
        mockWebSocket.ReceivedMessages.Add("any_message");

        // Act – run briefly; static behaviors should be sent without needing incoming messages
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TestTimeoutMs);

        try
        {
            await wsService.HandleConnectionAsync(mockWebSocket, "/ws/static-multi", cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert – both static-A and static-B were sent
        Assert.True(mockWebSocket.SentMessages.Count >= 3, $"Expected at least 3 messages (hello + 2), got {mockWebSocket.SentMessages.Count}");
        Assert.Contains(mockWebSocket.SentMessages, msg => msg.Contains("static-A"));
        Assert.Contains(mockWebSocket.SentMessages, msg => msg.Contains("static-B"));
    }
    // Mock WebSocket for testing
    private class MockWebSocket : WebSocket
    {
        public List<string> SentMessages { get; } = new();
        public List<string> ReceivedMessages { get; set; } = new();
        private int _receiveIndex = 0;

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => WebSocketState.Open;
        public override string? SubProtocol => null;

        public override void Abort()
        {
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            if (_receiveIndex >= ReceivedMessages.Count)
            {
                // Wait indefinitely if no more messages
                return Task.Delay(-1, cancellationToken).ContinueWith<WebSocketReceiveResult>(_ => null!);
            }

            var message = ReceivedMessages[_receiveIndex++];
            var bytes = Encoding.UTF8.GetBytes(message);
            bytes.CopyTo(buffer.Array!, buffer.Offset);
            return Task.FromResult(new WebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, true));
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            var message = Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count);
            SentMessages.Add(message);
            return Task.CompletedTask;
        }
    }
}
