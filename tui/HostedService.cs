using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Terminal.Gui;

namespace Esp32EmuConsole.Tui;


public class HostedService : BackgroundService
{
    private readonly Services.LogBuffer _logBuffer;
    private readonly ILogger<HostedService> _logger;
    // UI thread + app instance for coordination
    // private Thread? _uiThread;
    private readonly TaskCompletionSource _uiExited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public HostedService(Services.LogBuffer logBuffer, ILogger<HostedService> logger)
    {
        _logBuffer = logBuffer ?? throw new ArgumentNullException(nameof(logBuffer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // If we’re not in an interactive console, skip starting the TUI.
        if (!Environment.UserInteractive || Console.IsOutputRedirected)
        {
            _logger.LogInformation("TUI disabled (non-interactive console).");
            // Keep the hosted service alive until shutdown.
            try { await Task.Delay(System.Threading.Timeout.Infinite, ct); } catch (OperationCanceledException) { }
            return;
        }

        _logger.LogInformation("Starting TUI...");

        // Application.Init();

        // var top = Application.Top;

        // var win = new Window("Hello")
        // {
        //     X = 0, Y = 1, // Leave one row for the toplevel menu
        //     Width = Dim.Fill(),
        //     Height = Dim.Fill()
        // };
        // top.Add(win);

        // var label = new Label("Hello, Terminal.Gui!") 
        // {
        //     X = Pos.Center(),
        //     Y = Pos.Center()
        // };
        // win.Add(label);

        // var nameLabel = new Label(2, 2, "Name:");
        // var nameField = new TextField("")
        // {
        //     X = 14,
        //     Y = 2,
        //     Width = 40
        // };

        // var greetButton = new Button("Greet")
        // {
        //     X = 14,
        //     Y = 4
        // };
        // greetButton.Clicked += () =>
        // {
        //     MessageBox.Query(50, 7, "Greeting", $"Hello, {nameField.Text}", "Ok");
        // };

        // win.Add(nameLabel, nameField, greetButton);

        // Application.Run();
        // Application.Shutdown();


        Application.Init();

        var label = new Label("Press ANY key... (Ctrl+C to force quit)")
        {
            X = Pos.Center(),
            Y = Pos.Center()
        };

        Application.Top.Add(label);

        Application.Top.KeyPress += (e) =>
        {
            label.Text = $"Key detected: {e.KeyEvent.Key}";
            Application.Refresh();
        };

        Application.Run();
        Application.Shutdown();

        // When the host is stopping, request the TUI to stop
        // using var reg = ct.Register(StopTuiSafely);

        // // Wait until the UI exits (either user quits or host stops)
        // await _uiExited.Task;

        _logger.LogInformation("TUI exited.");
    }

//     private void RunTuiLoop(CancellationToken ct)
//     {
//         try
//         {
//             // Optional: apply a dark-ish theme (matches your example)
//             // You can switch to "Dark" or other built-in themes too.
//             // Terminal.Gui.Configuration.ConfigurationManager.RuntimeConfig = """{ "Theme": "Amber Phosphor" }""";
//             Terminal.Gui.Configuration.ConfigurationManager.Enable(ConfigLocations.All);

//             _app = Application.Create().Init();

//             _app.Run<MainWindow>();
//             // Build main window (tabs/logs placeholders)
//             // var window = new MainWindow(_logger);

//             // // Key handler for quick quit (Q or Ctrl+Q)
//             // window.KeyDown += (s, k) =>
//             // {
//             //     // if (k == Key.Q || k == (Key.Q | Key.CtrlMask))
//             //     // {
//             //     //     _app!.RequestStop();
//             //     //     k.Handled = true;
//             //     // }
//             //     _logger.LogInformation("Key pressed in TUI: {Key}", k);
//             // };

//             // // Run the UI loop (blocks on this thread)
//             // _app.Run(window);

//             // Normal shutdown path
//             // _app.Shutdown();
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex, "TUI crashed.");
//         }
//         finally
//         {
//             _app = null;
//             _uiExited.TrySetResult();
//         }
//     }
}