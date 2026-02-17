using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;

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

        Assert.Throws<ArgumentNullException>(() => new Services.WebSocketService(null!, _loggerFactory, rules));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenRulesIsNull()
    {
        var logger = _loggerFactory.CreateLogger<Services.WebSocketService>();

        Assert.Throws<ArgumentNullException>(() => new Services.WebSocketService(logger, _loggerFactory, null!));
    }

    [Fact]
    public async Task HandleConnectionAsync_LogsConnectionEstablished()
    {
        // Arrange
        var rulesJson = @"[]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);
        using var rules = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());
        var wsService = new Services.WebSocketService(_loggerFactory.CreateLogger<Services.WebSocketService>(), _loggerFactory, rules);

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
        var wsService = new Services.WebSocketService(_loggerFactory.CreateLogger<Services.WebSocketService>(), _loggerFactory, rules);

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
                ""type"": ""websocket"",
                ""uri"": ""/ws"",
                ""behavior"": ""echo""
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);
        using var rules = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());
        var wsService = new Services.WebSocketService(_loggerFactory.CreateLogger<Services.WebSocketService>(), _loggerFactory, rules);

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
                ""type"": ""websocket"",
                ""uri"": ""/ws/sensor"",
                ""behavior"": ""static"",
                ""webSocketResponse"": ""{\u0022temp\u0022:25.5}""
            }
        ]";
        var tempDir = CreateTempDirectoryWithRulesFile(rulesJson);
        using var rules = new Services.Rules(tempDir, _loggerFactory.CreateLogger<Services.Rules>());
        var wsService = new Services.WebSocketService(_loggerFactory.CreateLogger<Services.WebSocketService>(), _loggerFactory, rules);

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
        var wsService = new Services.WebSocketService(_loggerFactory.CreateLogger<Services.WebSocketService>(), _loggerFactory, rules);

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
