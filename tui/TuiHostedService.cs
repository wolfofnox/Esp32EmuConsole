
using Terminal.Gui;

namespace Esp32EmuConsole.Tui;

public class TuiHostedService : BackgroundService
{
    private readonly Services.LogBuffer _logBuffer;
    private readonly ILogger<TuiHostedService> _logger;

    public TuiHostedService(Services.LogBuffer logBuffer, ILogger<TuiHostedService> logger)
    {
        _logBuffer = logBuffer ?? throw new ArgumentNullException(nameof(logBuffer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        Application.Init();
        var top = Application.Top;

        var win = new Window("ESP32 Emulator Console")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1)
        };
        top.Add(win);

        var topBar = new Label("ESP32 Emulator is running...")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            Text = ""
        };
        win.Add(topBar);

        var tabs = new TabView()
        {
            X = 0,
            Y = Pos.Bottom(topBar),
            Width = Dim.Fill(),
            Height = Dim.Fill(1)
        };
        win.Add(tabs);

        var logTab = new View() {Width = Dim.Fill(), Height = Dim.Fill() };
        var httpTab = new View() {Width = Dim.Fill(), Height = Dim.Fill() };
        var wsTab = new View() {Width = Dim.Fill(), Height = Dim.Fill() };
        var rulesTab = new View() {Width = Dim.Fill(), Height = Dim.Fill() };
        var configTab = new View() {Width = Dim.Fill(), Height = Dim.Fill() };

        tabs.AddTab(new TabView.Tab("Logs", logTab), true);
        tabs.AddTab(new TabView.Tab("HTTP", httpTab), false);
        tabs.AddTab(new TabView.Tab("WebSocket", wsTab), false);
        tabs.AddTab(new TabView.Tab("Rules", rulesTab), false);
        tabs.AddTab(new TabView.Tab("Config", configTab), false);


        // Status bar with hotkeys — these work repeatedly regardless of focused view
        var statusBar = new StatusBar(new StatusItem[]
        {
            new StatusItem(Key.D1, "~1~ Logs", () => tabs.SelectedTab = tabs.Tabs.ElementAt(0)),
            new StatusItem(Key.D2, "~2~ HTTP", () => tabs.SelectedTab = tabs.Tabs.ElementAt(1)),
            new StatusItem(Key.D3, "~3~ WebSocket", () => tabs.SelectedTab = tabs.Tabs.ElementAt(2)),
            new StatusItem(Key.D4, "~4~ Rules", () => tabs.SelectedTab = tabs.Tabs.ElementAt(3)),
            new StatusItem(Key.D5, "~5~ Config", () => tabs.SelectedTab = tabs.Tabs.ElementAt(4)),
            new StatusItem(Key.q | Key.CtrlMask, "~^Q~ Quit", () => Application.RequestStop()),
            new StatusItem(Key.t, "~T~ Test", () => _logger.LogInformation("Test log entry from TUI"))
        })
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1
        };
        // Add the status bar to the top-level so its hotkeys are processed globally
        top.Add(statusBar);

        var logView = new TextView()
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 1,
            ReadOnly = true,
            WordWrap = true
        };
        logTab.Add(logView);

        // Debug: show initial focus and keypress info in the top bar
        _logger.LogInformation("TUI starting. Initial focused view: {focused}", top.Focused?.ToString() ?? "none");
        topBar.Text = "Focus: " + (top.Focused?.ToString() ?? "none") + "  |  LastKey: -";

        // Track focus changes (polling is simple and reliable across drivers)
        View? prevFocused = null;
        var prevKey = "-";
        Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(150), _ =>
        {
            try
            {
                var current = top.MostFocused;
                if (!ReferenceEquals(current, prevFocused))
                {
                    prevFocused = current;
                    var name = current?.ToString() ?? "none";
                    _logger.LogInformation("Focus changed -> {view}", name);
                    topBar.Text = "Focus: " + name + "  |  LastKey: " + prevKey;
                }
            }
            catch { }
            return true; // keep the timeout recurring
        });

        // Global key logging - subscribe via the top-level view's KeyDown event
        top.KeyDown += (e) =>
        {
            try
            {
                var key = e.ToString();
                _logger.LogInformation("Key pressed: {key}", key);
                Application.MainLoop.Invoke(() =>
                {
                    topBar.Text = "Focus: " + (prevFocused?.ToString() ?? "none") + "  |  LastKey: " + key;
                });
            }
            catch { }
        };

        // populate initial logs
        foreach (var l in _logBuffer.Snapshot())
        {
            logView.Text += l + Environment.NewLine;
        }

        // subscribe to new logs
        _logBuffer.NewLog += (line) =>
        {
            try
            {
                Application.MainLoop.Invoke(() =>
                {
                    logView.Text += line + Environment.NewLine;
                });
            }
            catch { }
        };

        ct.Register(() => Application.RequestStop());

        Application.Run();

        await Task.CompletedTask;
    }
}