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
    bool TryGetResponse(string method, string path, out HttpResponse? response);

    /// <summary>
    /// Reloads the rules from the rules file.
    /// This is thread-safe and uses write locks to prevent concurrent access during reload.
    /// </summary>
    void ReloadRules();
}
