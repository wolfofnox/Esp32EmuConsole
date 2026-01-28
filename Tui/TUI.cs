using Terminal.Gui;

namespace Esp32EmuConsole.Tui;


public class TUI
{
    private readonly ILogger<TUI> _logger;
    private readonly IServiceProvider _services;

    public TUI(IServiceProvider serviceProvider)
    {
        _services = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = _services.GetRequiredService<ILogger<TUI>>();
    }

    public void Run()
    {
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

        _logger.LogInformation("TUI exited.");
    }
}