namespace Esp32EmuConsole.Tui;

public class Configuration
{
    public bool showAppLogs { get; set; } = false;
    public bool showHttpTraffic { get; set; } = true;
    public bool showWebSocketTraffic { get; set; } = true;
    public bool showClients { get; set; } = false;
    public bool showStats { get; set; } = false;
    public bool tabView { get; set; } = false;
    public bool splitView { get; set; } = true;
 
}