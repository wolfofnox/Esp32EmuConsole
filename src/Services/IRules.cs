namespace Esp32EmuConsole.Services;

/// <summary>
/// Interface for the Rules service, enabling dependency injection and future UI editor integration.
/// </summary>
public interface IRules : IDisposable
{
    /// <summary>
    /// Gets a read-only snapshot of the current rule list.
    /// This is thread-safe and returns a copy of the current rules.
    /// </summary>
    IReadOnlyList<Rule> GetRules();

    /// <summary>
    /// Attempts to get a response for the specified HTTP method and path.
    /// This is thread-safe and uses read locks for concurrent access.
    /// </summary>
    /// <param name="method">The HTTP method (e.g., GET, POST).</param>
    /// <param name="path">The request path.</param>
    /// <param name="response">The fixed response if found, null otherwise.</param>
    /// <returns>True if a rule was found for the method and path; otherwise, false.</returns>
    bool GetHttpResponse(string method, string path, out HttpResponse? response);

    /// <summary>
    /// Attempts to get a WebSocket response for the specified path and incoming message.
    /// This is thread-safe and uses read locks for concurrent access.
    /// </summary>
    /// <param name="path">The WebSocket path.</param>
    /// <param name="incomingMessage">The incoming WebSocket message.</param>
    /// <param name="response">The WebSocket response if found, null otherwise.</param>
    /// <returns>True if a rule was found for the path and incoming message; otherwise, false.</returns>
    bool GetWebSocketResponse(string path, string incomingMessage, out WebSocketResponse? response);

    /// <summary>
    /// Attempts to get a WebSocket response with "interval" behavior for the specified path.
    /// This is thread-safe and uses read locks for concurrent access.
    /// </summary>
    /// <param name="path">The WebSocket path.</param>
    /// <param name="response">The WebSocket response if found, null otherwise.</param>
    /// <returns>True if a rule was found for the path; otherwise, false.</returns>
    bool GetWebSocketIntervalResponse(string path, out WebSocketResponse? response);

    /// <summary>
    /// Reloads the rules from the rules file.
    /// This is thread-safe and uses write locks to prevent concurrent access during reload.
    /// </summary>
    void ReloadRules();
}
