using System.Net.WebSockets;
using System.Text;
using WS = System.Net.WebSockets.WebSocket;

namespace Esp32EmuConsole.Services.WebSocket;

/// <summary>
/// Handles WebSocket connections by looking up the requested path in <c>rules.json</c>
/// and applying the configured behavior:
/// <list type="bullet">
///   <item><b>echo</b> — reflects every incoming message back to the sender.</item>
///   <item><b>static</b> — sends a fixed text or binary payload per incoming message.</item>
///   <item><b>interval</b> — pushes a text or binary payload on a recurring timer.</item>
/// </list>
/// All connections receive an initial <c>{"type":"hello","msg":"Connected"}</c> greeting.
/// </summary>
public class WebSocketService
{
    private const int MaxMessageBufferSize = 4096;
    
    private readonly ILogger<WebSocketService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IRules _rules;

    public WebSocketService(ILogger<WebSocketService> logger, ILoggerFactory loggerFactory, IRules rules)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        
        // Check for WebSocket rules at "/" and warn
        var rootWsRules = _rules.GetRules().Where(r => 
            r.Uri == "/" && r.Response?.Ws != null).ToList();
        
        if (rootWsRules.Any())
        {
            _logger.LogWarning(
                "WebSocket rules defined at \"/\" will be ignored. This path is reserved for Vite HMR. " +
                "Found {Count} rule(s) at this path.", rootWsRules.Count);
        }
    }

    public async Task HandleConnectionAsync(WS webSocket, string path, CancellationToken cancellationToken = default)
    {
        var wsLogger = _loggerFactory.CreateLogger("WS");
        wsLogger.LogInformation("Connection opened: {Path}", path);
        _logger.LogInformation("WebSocket connection opened: {Path}", path);

        try
        {
            // Send hello message
            var hello = System.Text.Json.JsonSerializer.Serialize(new { type = "hello", msg = "Connected" });
            var helloBytes = Encoding.UTF8.GetBytes(hello);
            await webSocket.SendAsync(helloBytes, WebSocketMessageType.Text, true, cancellationToken);
            wsLogger.LogInformation("[SENT] {Message}", hello);

            // Start interval task if configured
            var intervalCts = new List<CancellationTokenSource>();

            if (_rules.TryGetWebSocketIntervalResponse(path, out var intervalResponses) && intervalResponses != null)
            {
                foreach (var resp in intervalResponses)
                {
                    if (resp.IntervalMs.HasValue)
                    {
                        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        intervalCts.Add(cts);
                        _ = StartIntervalMessageAsync(webSocket, resp, wsLogger, cts.Token);
                    }
                }
            }

            var buffer = new byte[MaxMessageBufferSize];
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(buffer, cancellationToken);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    wsLogger.LogInformation("Connection closed: {Path}", path);
                    _logger.LogInformation("WebSocket connection closed: {Path}", path);
                    break;
                }

                // Log the bare received message
                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var hexMessage = Convert.ToHexString(buffer, 0, result.Count);
                    wsLogger.LogInformation("[RECV BINARY HEX] {Message}", hexMessage);
                }
                else
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    wsLogger.LogInformation("[RECV] {Message}", message);
                }

                if (_rules.TryGetWebSocketResponse(path, Encoding.UTF8.GetString(buffer, 0, result.Count), out var responses) && responses != null)
                {
                    foreach (var resp in responses)
                    {   
                        switch (resp.Behavior?.ToLowerInvariant())
                        {
                            case "echo":
                                await webSocket.SendAsync(buffer[..result.Count], result.MessageType, true, cancellationToken);
                                wsLogger.LogInformation("[ECHOED] {Message}", result.MessageType == WebSocketMessageType.Binary
                                    ? Convert.ToHexString(buffer, 0, result.Count)
                                    : Encoding.UTF8.GetString(buffer, 0, result.Count));
                                break;
                            case "static" when !string.IsNullOrEmpty(resp.Binary):
                                await webSocket.SendAsync(Convert.FromHexString(resp.Binary), WebSocketMessageType.Binary, true, cancellationToken);
                                wsLogger.LogInformation("[SENT BINARY HEX] {Message}", resp.Binary);
                                break;
                            case "static" when !string.IsNullOrEmpty(resp.Text):
                                await webSocket.SendAsync(Encoding.UTF8.GetBytes(resp.Text), WebSocketMessageType.Text, true, cancellationToken);
                                wsLogger.LogInformation("[SENT] {Message}", resp.Text);
                                break;
                            default:
                                _logger.LogWarning("Unsupported WebSocket behavior: {Behavior} for path: {Path}", resp.Behavior, path);
                                break;
                        };
                    }
                }
            }

            foreach (var cts in intervalCts)
            {
                cts.Cancel();
            }

            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            wsLogger.LogInformation("Connection cancelled: {Path}", path);
            _logger.LogInformation("WebSocket connection cancelled: {Path}", path);
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
            _logger.LogInformation("WebSocket connection finally closed: {Path}", path);
        }
    }

    private async Task StartIntervalMessageAsync(WS webSocket, WebSocketResponse wsResponse, ILogger wsLogger, CancellationToken cancellationToken)
    {
        var responseData = wsResponse.Text ?? wsResponse.Binary;

        if (!wsResponse.IntervalMs.HasValue || wsResponse.IntervalMs.Value <= 0)  
        {  
            wsLogger.LogWarning("Interval response IntervalMs must be greater than 0. Skipping interval message.");  
            return;  
        } 

        try
        {
            while (!cancellationToken.IsCancellationRequested && webSocket.State == WebSocketState.Open)
            {
                await Task.Delay(wsResponse.IntervalMs.Value, cancellationToken);
                
                if (webSocket.State == WebSocketState.Open && !string.IsNullOrEmpty(responseData))
                {
                    byte[] responseBytes;
                    WebSocketMessageType messageType;
                    
                    if (!string.IsNullOrEmpty(wsResponse.Binary))
                    {
                        responseBytes = Convert.FromHexString(wsResponse.Binary);
                        messageType = WebSocketMessageType.Binary;
                        await webSocket.SendAsync(responseBytes, messageType, true, cancellationToken);
                        
                        var hexMessage = Convert.ToHexString(responseBytes);
                        wsLogger.LogInformation("[SENT INTERVAL BINARY HEX] {Message}", hexMessage);
                    }
                    else
                    {
                        responseBytes = Encoding.UTF8.GetBytes(responseData);
                        messageType = WebSocketMessageType.Text;
                        await webSocket.SendAsync(responseBytes, messageType, true, cancellationToken);
                        
                        wsLogger.LogInformation("[SENT INTERVAL] {Message}", responseData);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when connection closes
        }
    }
}
