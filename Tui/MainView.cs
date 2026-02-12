using Esp32EmuConsole.Utilities;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.Gui.App;
using System.Runtime.Intrinsics.X86;

namespace Esp32EmuConsole.Tui;

class MainView : Runnable
{
    private readonly LogBuffer _logBuffer;
    private MenuBar _menu;
    private FrameView? _appLogFrame;
    private FrameView? _httpLogFrame;
    private FrameView? _wsLogFrame;
    private FrameView? _clientsFrame;
    private FrameView? _statsFrame;
    private readonly Window _mainWindow;
    private readonly Configuration _config;
    private readonly ILogger<MainView> _logger;
    public MainView(LogBuffer logBuffer, Configuration config, ILogger<MainView> logger)
    {
        _logBuffer = logBuffer ?? throw new ArgumentNullException(nameof(logBuffer));

        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _mainWindow = new Window()
        {
            Title = "Esp32EmuConsole TUI",

            X = 0,
            Y = 2,

            Width = Dim.Fill(),
            Height = Dim.Fill()-1
        };

        _menu = CreateMenu();

        // create basic empty views for logs, clients and stats
        _appLogFrame = CreateLogFrame("App Logs");
        _httpLogFrame = CreateLogFrame("HTTP Logs");
        _wsLogFrame = CreateLogFrame("WebSocket Logs");
        _clientsFrame = CreateClientsFrame();
        _statsFrame = CreateStatsFrame();
        _mainWindow.Add(_appLogFrame, _httpLogFrame, _wsLogFrame, _clientsFrame, _statsFrame);
        Add(_mainWindow, _menu);

        UpdateLayout();
    }

    private FrameView CreateLogFrame(string title)
    {
        var frame = new FrameView()
        {
            Title = title,
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        var tv = new TextView()
        {
            ReadOnly = true,
            WordWrap = false,
            Multiline = true,
            Text = "",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        frame.Add(tv);
        return frame;
    }

    private FrameView CreateClientsFrame()
    {
        var frame = new FrameView()
        {
            Title = "Connected Clients",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        var lv = new ListView() { Text = "Not implemented yet", X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        frame.Add(lv);
        return frame;
    }

    private FrameView CreateStatsFrame()
    {
        var frame = new FrameView()
        {
            Title = "Stats",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        // simple placeholders
        frame.Add(new Label() { Text = "No stats available yet.",X = 0, Y = 0 });
        return frame;
    }

    private MenuBar CreateMenu()
    {
        return new MenuBar(new MenuBarItem[]
        {
            new MenuBarItem("(use Alt)", new MenuItem[0]),
            new MenuBarItem("_File", new MenuItem[]
            {
                new MenuItem("_Quit", "", () => 
                {
                    App!.RequestStop();
                })
            }),
            new MenuBarItem("_Edit", new MenuItem[]
            {
                new MenuItem("_Settings", "", () =>
                {
                    MessageBox.Query(App!, "Settings", "Settings dialog not implemented yet.", "Ok");
                }),
                new MenuItem("Static _Responses", "", () =>
                {
                    MessageBox.Query(App!, "Static Responses", "Static responses dialog not implemented yet.", "Ok");
                })
            }),
            new MenuBarItem("_View", new MenuItem[]
            {
                new MenuItem((_config.showAppLogs ? "[x] " : "[ ] ") + "_Logs", "", () => { _config.showAppLogs = !_config.showAppLogs; UpdateLayout(); RebuildMenu(); }),
                new MenuItem((_config.showClients ? "[x] " : "[ ] ") + "_Clients", "", () => { _config.showClients = !_config.showClients; UpdateLayout(); RebuildMenu(); }),
                new MenuItem((_config.showStats ? "[x] " : "[ ] ") + "_Stats", "", () => { _config.showStats = !_config.showStats; UpdateLayout(); RebuildMenu(); }),
                new MenuItem((_config.showHttpTraffic ? "[x] " : "[ ] ") + "_HTTP traffic", "", () => { _config.showHttpTraffic = !_config.showHttpTraffic; UpdateLayout(); RebuildMenu(); }),
                new MenuItem((_config.showWebSocketTraffic ? "[x] " : "[ ] ") + "_WebSocket traffic", "", () => { _config.showWebSocketTraffic = !_config.showWebSocketTraffic; UpdateLayout(); RebuildMenu(); }),
                // null,
                // new MenuItem((_config.tabView ? "[x] " : "[ ] ") + "_Tab view", "", () => { _config.tabView = true; _config.splitView = false; UpdateLayout(); RebuildMenu(); }),
                // new MenuItem((_config.splitView ? "[x] " : "[ ] ") + "S_plit view", "", () => { _config.splitView = true; _config.tabView = false; UpdateLayout(); RebuildMenu(); }),
                null,
                new MenuItem("Clea_r logs", "", () => { _logger.LogWarning("Clear logs action triggered - not implemented yet.");}),
            }),
        })
        {
            Y = 1
        };
    }

    private void RebuildMenu()
    {
        if (_menu != null)
            Remove(_menu);
        _menu = CreateMenu();
        Add(_menu);
    }

    private void UpdateLayout()
    {
        // Show or hide the basic views according to config flags.
        if (_appLogFrame != null) _appLogFrame.Visible = _config.showAppLogs;
        if (_httpLogFrame != null) _httpLogFrame.Visible = _config.showHttpTraffic;
        if (_wsLogFrame != null) _wsLogFrame.Visible = _config.showWebSocketTraffic;
        if (_clientsFrame != null) _clientsFrame.Visible = _config.showClients;
        if (_statsFrame != null) _statsFrame.Visible = _config.showStats;

        // Arrange visible log panes side-by-side across the top area. Clients/Stats are placed below.
        var visibleTop = new List<FrameView>();
        if (_config.showAppLogs && _appLogFrame != null) visibleTop.Add(_appLogFrame);
        if (_config.showHttpTraffic && _httpLogFrame != null) visibleTop.Add(_httpLogFrame);
        if (_config.showWebSocketTraffic && _wsLogFrame != null) visibleTop.Add(_wsLogFrame);

        // Layout clients and stats in bottom area
        var visibleBottom = new List<FrameView>();
        if (_config.showClients && _clientsFrame != null) visibleBottom.Add(_clientsFrame);
        if (_config.showStats && _statsFrame != null) visibleBottom.Add(_statsFrame);

        int topPercent = visibleBottom.Count > 0 ? 70 : 100;

        int bottomStart = topPercent;
        int bottomHeight = 100 - topPercent;

        // Layout top log panes
        if (visibleTop.Count > 0)
        {
            for (int i = 0; i < visibleTop.Count; i++)
            {
                var pane = visibleTop[i];
                pane.X = Pos.Percent((int)float.Round(i * (100f / visibleTop.Count)));
                pane.Y = 0;
                pane.Width = Dim.Percent((int)float.Round(100f / visibleTop.Count));
                if (visibleTop.Count == 3 && i == 1)
                {
                    // Make middle pane slightly wider when 3 panes are visible to reduce wasted space
                    pane.Width = Dim.Percent((int)float.Round(100f / visibleTop.Count) + 1);
                }
                pane.Height = Dim.Percent(topPercent);
            }
        }

        if (visibleBottom.Count == 1)
        {
            var f = visibleBottom[0];
            f.X = 0;
            f.Y = Pos.Percent(bottomStart);
            f.Width = Dim.Fill();
            f.Height = Dim.Percent(bottomHeight);
        }
        else if (visibleBottom.Count == 2)
        {
            visibleBottom[0].X = 0;
            visibleBottom[0].Y = Pos.Percent(bottomStart);
            visibleBottom[0].Width = Dim.Percent(50);
            visibleBottom[0].Height = Dim.Fill();

            visibleBottom[1].X = Pos.Percent(50);
            visibleBottom[1].Y = Pos.Percent(bottomStart);
            visibleBottom[1].Width = Dim.Percent(50);
            visibleBottom[1].Height = Dim.Fill();
        }
    }
}