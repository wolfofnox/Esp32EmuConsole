using System.Net.WebSockets;
using System.Text;
using WS = System.Net.WebSockets.WebSocket;

namespace Esp32EmuConsole.Services.WebSocket;

public class WebSocketService
{
    private const int MaxMessageBufferSize = 4096;
    
    private readonly ILogger<WebSocketService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IRules _rules;
    private readonly Dictionary<string, CancellationTokenSource> _intervalTasks = new();

    public WebSocketService(ILogger<WebSocketService> logger, ILoggerFactory loggerFactory, IRules rules)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public async Task HandleConnectionAsync(WS webSocket, string path, CancellationToken cancellationToken = default)
    {
        var wsLogger = _loggerFactory.CreateLogger("WS");
        wsLogger.LogInformation("Connection opened: {Path}", path);

        try
        {
            // Send hello message
            var hello = System.Text.Json.JsonSerializer.Serialize(new { type = "hello", msg = "Connected" });
            var helloBytes = Encoding.UTF8.GetBytes(hello);
            await webSocket.SendAsync(helloBytes, WebSocketMessageType.Text, true, cancellationToken);

            // Start interval task if configured
            var intervalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = StartIntervalMessagesAsync(webSocket, path, intervalCts.Token);

            var buffer = new byte[MaxMessageBufferSize];
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(buffer, cancellationToken);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    wsLogger.LogInformation("Connection closed: {Path}", path);
                    break;
                }

                // Log the bare message
                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var hexMessage = Convert.ToHexString(buffer, 0, result.Count);
                    wsLogger.LogInformation("[BINARY HEX] {Message}", hexMessage);
                }
                else
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    wsLogger.LogInformation("{Message}", message);
                }

                var (responseMessage, messageType) = GetResponseForPath(path, buffer, result.Count, result.MessageType);
                if (responseMessage != null)
                {
                    await webSocket.SendAsync(responseMessage, messageType, true, cancellationToken);
                }
            }

            intervalCts.Cancel();

            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            wsLogger.LogInformation("Connection cancelled: {Path}", path);
        }
        catch (WebSocketException ex)
        {
            _logger.LogError(ex, "WebSocket error on path: {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in WebSocket handler for path: {Path}", path);
        }
        finally
        {
            wsLogger.LogInformation("Connection closed: {Path}", path);
        }
    }

    private async Task StartIntervalMessagesAsync(WS webSocket, string path, CancellationToken cancellationToken)
    {
        var rules = _rules.GetRules();
        var wsRule = rules.FirstOrDefault(r => 
            (r.Type?.Equals("websocket", StringComparison.OrdinalIgnoreCase) == true || string.IsNullOrEmpty(r.Type)) && 
            r.Uri?.Equals(path, StringComparison.OrdinalIgnoreCase) == true &&
            r.Behavior?.Equals("interval", StringComparison.OrdinalIgnoreCase) == true &&
            r.IntervalMs.HasValue);

        if (wsRule == null || !wsRule.IntervalMs.HasValue)
        {
            return;
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested && webSocket.State == WebSocketState.Open)
            {
                await Task.Delay(wsRule.IntervalMs.Value, cancellationToken);
                
                if (webSocket.State == WebSocketState.Open && !string.IsNullOrEmpty(wsRule.WebSocketResponse))
                {
                    var responseBytes = Encoding.UTF8.GetBytes(wsRule.WebSocketResponse);
                    await webSocket.SendAsync(responseBytes, WebSocketMessageType.Text, true, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when connection closes
        }
    }

    private (byte[]? response, WebSocketMessageType messageType) GetResponseForPath(string path, byte[] buffer, int count, WebSocketMessageType receivedType)
    {
        var rules = _rules.GetRules();
        var wsRule = rules.FirstOrDefault(r => 
            (r.Type?.Equals("websocket", StringComparison.OrdinalIgnoreCase) == true || string.IsNullOrEmpty(r.Type)) && 
            r.Uri?.Equals(path, StringComparison.OrdinalIgnoreCase) == true);

        if (wsRule == null)
        {
            return (null, WebSocketMessageType.Text);
        }

        var behavior = wsRule.Behavior?.ToLowerInvariant();
        return behavior switch
        {
            "echo" => (buffer[..count], receivedType),
            "static" when !string.IsNullOrEmpty(wsRule.WebSocketResponse) => 
                (Encoding.UTF8.GetBytes(wsRule.WebSocketResponse), WebSocketMessageType.Text),
            "binary" when !string.IsNullOrEmpty(wsRule.WebSocketResponse) => 
                (Convert.FromHexString(wsRule.WebSocketResponse), WebSocketMessageType.Binary),
            _ => (null, WebSocketMessageType.Text)
        };
    }
}
