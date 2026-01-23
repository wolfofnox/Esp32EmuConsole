
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
            Height = Dim.Fill()
        };
        top.Add(win);

        var topBar = new Label("ESP32 Emulator is running...")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            Text = "Host: http://localhost:5000  |  Proxy: (auto)  |  WS: 0  |  Mode: Side-by-side"
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
        win.Add(statusBar);

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