namespace Esp32EmuConsole.Tui;

/// <summary>
/// Holds the user-adjustable display preferences for the Terminal UI.
/// Each flag controls whether the corresponding panel is visible.
/// </summary>
public class Configuration
{
    /// <summary>When <see langword="true"/>, the App Logs panel is shown on startup.</summary>
    public bool showAppLogs { get; set; } = false;

    /// <summary>When <see langword="true"/>, the HTTP Logs panel is shown on startup.</summary>
    public bool showHttpTraffic { get; set; } = true;

    /// <summary>When <see langword="true"/>, the WebSocket Logs panel is shown on startup.</summary>
    public bool showWebSocketTraffic { get; set; } = true;

    /// <summary>When <see langword="true"/>, the Connected Clients panel is shown on startup.</summary>
    public bool showClients { get; set; } = false;

    /// <summary>When <see langword="true"/>, the Stats panel is shown on startup.</summary>
    public bool showStats { get; set; } = false;

    /// <summary>Reserved for future tab-based layout. Currently unused.</summary>
    public bool tabView { get; set; } = false;

    /// <summary>When <see langword="true"/>, panels are arranged in a side-by-side split layout.</summary>
    public bool splitView { get; set; } = true;
 
}