using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.Gui.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Esp32EmuConsole.Utilities;

namespace Esp32EmuConsole.Tui;


public class TUI
{
    private readonly ILogger<TUI> _logger;
    private readonly IServiceProvider _services;
    private readonly IApplication _app;
    private readonly Configuration _config;

    public TUI(IServiceProvider serviceProvider)
    {
        _services = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = _services.GetRequiredService<ILogger<TUI>>();
        _config = _services.GetRequiredService<Configuration>();
        _app = Application.Create();

        // Override the default configuration for the application to use the Amber Phosphor theme
        Terminal.Gui.Configuration.ConfigurationManager.RuntimeConfig = """{ "Theme": "Amber Phosphor" }""";
        Terminal.Gui.Configuration.ConfigurationManager.Enable (ConfigLocations.All);
    }

    public void Run()
    {
        _app.Init();

        try
        {
            var mainView = new MainView(_config, _services.GetRequiredService<ILogger<MainView>>());
            _app.Run(mainView);
        }
        finally
        {
            _logger.LogInformation("TUI is shutting down.");
            _app.Dispose();
        }

        // var topLabel = new Label("Terminal.Gui Application - Press Alt for Menu")
        // {
        //     X = 0,
        //     Y = 0,
        //     Width = Dim.Fill(),
        //     Height = 1,
        //     ColorScheme = new ColorScheme()
        //     {
        //         Normal = Application.Driver.MakeAttribute(Color.Black, Color.Gray)
        //     }
        // };
        // top.Add(topLabel);


        // var textView = new TextView()
        // {
        //     ReadOnly = true,
        //     Width = Dim.Fill(),
        //     Height = Dim.Fill()
        // };

        // if (_logBuffer is null)
        // {
        //     textView.Text = "No LogBuffer registered in services.";
        //     return;
        // }

        // _logBuffer.NewLog += handler;

        // win.Add(textView);

        // var label = new Label("Keypress: ")
        // {
        //     X = 0,
        //     Y = Pos.AnchorEnd(1),
        //     Width = Dim.Fill(),
        //     Height = 1,
        //     ColorScheme = new ColorScheme()
        //     {
        //         Normal = Application.Driver.MakeAttribute(Color.Black, Color.Gray)
        //     }
        // };

        // top.Add(label);

        // // textView.SetFocus();

        // top.KeyPress += (e) =>
        // {
        //     if (e.KeyEvent.Key == (Key.CtrlMask | Key.C))
        //     {
        //         e.Handled = true;
        //         Application.RequestStop();
        //     }
        //     label.Text = $"Keypress (top): {e.KeyEvent.Key}";
        //     Application.Refresh();
        // };

        // textView.KeyPress += (e) =>
        // {
        //     if (e.KeyEvent.Key == Key.G)
        //     {
        //         e.Handled = true;
        //         MessageBox.Query(50, 7, "Key Pressed", "You pressed 'G' in the TextView!", "Ok");
        //     }
        //     label.Text = $"Keypress (textView): {e.KeyEvent.Key}";
        // };

        // Application.Run();
        // Application.Shutdown();

        _logger.LogInformation("TUI exited.");
    }

}