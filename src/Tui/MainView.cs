using Esp32EmuConsole.Utilities;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.Gui.App;

namespace Esp32EmuConsole.Tui;

class MainView : Runnable
{
    private MenuBar _menu;
    private FrameView? _appLogFrame;
    private FrameView? _httpLogFrame;
    private FrameView? _wsLogFrame;
    private FrameView? _clientsFrame;
    private FrameView? _statsFrame;
    private TextView? _appLogView;
    private TextView? _httpLogView;
    private TextView? _wsLogView;
    private readonly Window _mainWindow;
    private readonly Configuration _config;
    private readonly ILogger<MainView> _logger;
    private readonly LogBuffer _appLogBuffer;
    private readonly LogBuffer _httpLogBuffer;
    private readonly LogBuffer _wsLogBuffer;
    private LogFilterState _currentFilter = LogFilterState.Empty;
    
    public MainView(Configuration config, ILogger<MainView> logger, LogBuffer appLogBuffer, LogBuffer httpLogBuffer, LogBuffer wsLogBuffer)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _appLogBuffer = appLogBuffer ?? throw new ArgumentNullException(nameof(appLogBuffer));
        _httpLogBuffer = httpLogBuffer ?? throw new ArgumentNullException(nameof(httpLogBuffer));
        _wsLogBuffer = wsLogBuffer ?? throw new ArgumentNullException(nameof(wsLogBuffer));

        _mainWindow = new Window()
        {
            Title = "Esp32EmuConsole",

            X = 0,
            Y = 1,

            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        _menu = CreateMenu();

        // create basic empty views for logs, clients and stats
        _appLogFrame = CreateLogFrame("App Logs", _appLogBuffer, out _appLogView);
        _httpLogFrame = CreateLogFrame("HTTP Logs", _httpLogBuffer, out _httpLogView);
        _wsLogFrame = CreateLogFrame("WebSocket Logs", _wsLogBuffer, out _wsLogView);
        _clientsFrame = CreateClientsFrame();
        _statsFrame = CreateStatsFrame();
        _mainWindow.Add(_appLogFrame, _httpLogFrame, _wsLogFrame, _clientsFrame, _statsFrame);
        Add(_menu, _mainWindow);

        UpdateLayout();
    }

    private FrameView CreateLogFrame(string title, LogBuffer logBuffer, out TextView textView)
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
            Text = string.Join("\n", logBuffer.Snapshot()),
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        
        textView = tv;
        
        // Subscribe to new log events to update the view with live data when visible
        logBuffer.NewLog += (line) =>
        {
            if (frame.Visible && App != null && _currentFilter.Matches(line))
            {
                App.Invoke(() =>
                {
                    tv.Text += "\n" + line;
                    // Autoscroll to the end
                    tv.MoveEnd();
                });
            }
        };
        
        // Subscribe to visibility changes to refresh with snapshot when toggled on
        frame.VisibleChanged += (sender, args) =>
        {
            if (frame.Visible && App != null)
            {
                App.Invoke(() =>
                {
                    tv.Text = string.Join("\n", logBuffer.Snapshot().Where(l => _currentFilter.Matches(l)));
                    // Autoscroll to the end
                    tv.MoveEnd();
                });
            }
        };
        
        frame.Add(tv);
        return frame;
    }

    private void ApplyFilterToAll()
    {
        App?.Invoke(() =>
        {
            ApplyFilterToView(_appLogFrame, _appLogView, _appLogBuffer);
            ApplyFilterToView(_httpLogFrame, _httpLogView, _httpLogBuffer);
            ApplyFilterToView(_wsLogFrame, _wsLogView, _wsLogBuffer);
        });
    }

    private void ApplyFilterToView(FrameView? frame, TextView? view, LogBuffer buffer)
    {
        if (frame?.Visible == true && view != null)
        {
            view.Text = string.Join("\n", buffer.Snapshot().Where(l => _currentFilter.Matches(l)));
            view.MoveEnd();
        }
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
        var filterIndicator = _currentFilter.IsActive ? " [filtered]" : "";
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
                null,
                new MenuItem("Clea_r logs", "", () => 
                { 
                    _appLogBuffer.Clear();
                    _httpLogBuffer.Clear();
                    _wsLogBuffer.Clear();
                    
                    // Clear the text in all visible log views
                    ClearLogViews();
                    _logger.LogInformation("All logs cleared by user.");
                }),
            }),
            new MenuBarItem("_Search", new MenuItem[]
            {
                new MenuItem("Search / _Filter Logs..." + filterIndicator, "", () =>
                {
                    var newFilter = SearchFilterDialog.Show(App!, _currentFilter);
                    if (newFilter != null)
                    {
                        _currentFilter = newFilter;
                        ApplyFilterToAll();
                        RebuildMenu();
                    }
                }),
                new MenuItem("_Clear Filter", "", () =>
                {
                    _currentFilter = LogFilterState.Empty;
                    ApplyFilterToAll();
                    RebuildMenu();
                }),
            }),
        })
        { };
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

    private void ClearLogViews()
    {
        // Clear text in each log TextView
        if (_appLogView != null) _appLogView.Text = "";
        if (_httpLogView != null) _httpLogView.Text = "";
        if (_wsLogView != null) _wsLogView.Text = "";
    }
}