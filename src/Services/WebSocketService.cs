using System.Net.WebSockets;
using System.Text;

namespace Esp32EmuConsole.Services;

public class WebSocketService
{
    private readonly ILogger<WebSocketService> _logger;
    private readonly IRules _rules;

    public WebSocketService(ILogger<WebSocketService> logger, IRules rules)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public async Task HandleConnectionAsync(WebSocket webSocket, string path, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("WebSocket connection established for path: {Path}", path);

        try
        {
            // Send hello message
            var hello = System.Text.Json.JsonSerializer.Serialize(new { type = "hello", msg = "Connected" });
            var helloBytes = Encoding.UTF8.GetBytes(hello);
            await webSocket.SendAsync(helloBytes, WebSocketMessageType.Text, true, cancellationToken);

            var buffer = new byte[4096];
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(buffer, cancellationToken);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("WebSocket close message received for path: {Path}", path);
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                _logger.LogInformation("Received WS message on {Path}: {Message}", path, message);

                var responseMessage = GetResponseForPath(path, message);
                if (responseMessage != null)
                {
                    var responseBytes = Encoding.UTF8.GetBytes(responseMessage);
                    await webSocket.SendAsync(responseBytes, WebSocketMessageType.Text, true, cancellationToken);
                    _logger.LogInformation("Sent WS response on {Path}: {Response}", path, responseMessage);
                }
            }

            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("WebSocket connection cancelled for path: {Path}", path);
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
            _logger.LogInformation("WebSocket connection closed for path: {Path}", path);
        }
    }

    private string? GetResponseForPath(string path, string message)
    {
        var rules = _rules.GetRules();
        var wsRule = rules.FirstOrDefault(r => 
            r.Type?.Equals("websocket", StringComparison.OrdinalIgnoreCase) == true && 
            r.Path?.Equals(path, StringComparison.OrdinalIgnoreCase) == true);

        if (wsRule == null)
        {
            return null;
        }

        var behavior = wsRule.Behavior?.ToLowerInvariant();
        return behavior switch
        {
            "echo" => message,
            "static" => wsRule.WebSocketResponse,
            _ => null
        };
    }
}
