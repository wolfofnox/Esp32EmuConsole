using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Terminal.Gui.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Esp32EmuConsole.Utilities;
using Services = Esp32EmuConsole.Services;

namespace Esp32EmuConsole.Tui;


/// <summary>
/// Entry point for the Terminal.Gui-based user interface.
/// Initializes the Terminal.Gui application, applies the Amber Phosphor colour theme,
/// constructs the <see cref="MainView"/>, and blocks on the UI event loop until the
/// user requests a quit.
/// </summary>
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
            var appLogBuffer = _services.GetRequiredKeyedService<LogBuffer>("AppLogBuffer");
            var httpLogBuffer = _services.GetRequiredKeyedService<LogBuffer>("HttpLogBuffer");
            var wsLogBuffer = _services.GetRequiredKeyedService<LogBuffer>("WsLogBuffer");
            var vite = _services.GetRequiredService<Services.Vite>();
            
            var mainView = new MainView(
                _config, 
                _services.GetRequiredService<Services.WebServer.Configuration>(),
                _services.GetRequiredService<ILogger<MainView>>(),
                appLogBuffer,
                httpLogBuffer,
                wsLogBuffer,
                vite
            );
            _app.Run(mainView);
        }
        finally
        {
            _logger.LogInformation("TUI is shutting down.");
            _app.Dispose();
        }

        _logger.LogInformation("TUI exited.");
    }

}