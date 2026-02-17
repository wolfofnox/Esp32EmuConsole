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
            var appLogBuffer = _services.GetRequiredKeyedService<LogBuffer>("AppLogBuffer");
            var httpLogBuffer = _services.GetRequiredKeyedService<LogBuffer>("HttpLogBuffer");
            var wsLogBuffer = _services.GetRequiredKeyedService<LogBuffer>("WsLogBuffer");
            
            var mainView = new MainView(
                _config, 
                _services.GetRequiredService<ILogger<MainView>>(),
                appLogBuffer,
                httpLogBuffer,
                wsLogBuffer
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