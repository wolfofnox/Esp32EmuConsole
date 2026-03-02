using Esp32EmuConsole;
using Esp32EmuConsole.Services.WebSocket;

namespace Esp32EmuConsole.Services.WebServer;

/// <summary>
/// Configures and manages the ASP.NET Core <see cref="WebApplication"/> pipeline:
/// <list type="bullet">
///   <item>Registers the <see cref="Middleware.ResponseLogger"/> and <see cref="Middleware.StaticResponse"/> middleware.</item>
///   <item>Maps WebSocket connections to <see cref="WebSocketService"/>.</item>
///   <item>Adds the YARP reverse proxy as a catch-all fallback to Vite.</item>
/// </list>
/// </summary>
class WebServer
{
    private readonly ILogger<WebServer> _logger;
    private readonly Services.Vite _vite;
    private readonly Services.IRules _rules;
    private readonly WebSocket.WebSocketService _wsService;
    private readonly Services.WebServer.Configuration _config;
    private readonly WebApplication _app;
    private Task? _serverTask;

    public WebServer(WebApplication app)
    {
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _logger = _app.Services.GetRequiredService<ILogger<WebServer>>();
        _vite = _app.Services.GetRequiredService<Services.Vite>();
        _rules = _app.Services.GetRequiredService<Services.IRules>();
        _wsService = _app.Services.GetRequiredService<WebSocket.WebSocketService>();
        _config = _app.Services.GetRequiredService<Services.WebServer.Configuration>();
    }
    public void Configure()
    { 
        _app.UseWebSockets();

        // Simple request logging
        _app.UseMiddleware<Middleware.ResponseLogger>();

        // Use the static response middleware (short-circuits matching endpoints)
        _app.UseMiddleware<Middleware.StaticResponse>();

        // WebSocket handling with service (handles all WebSocket requests)
        _app.MapWs(_wsService);

        // Reverse proxy for all remaining requests
        _app.UseRouting();
        _app.UseEndpoints(endpoints =>
        {
            endpoints.MapReverseProxy();
        });
    }

    public Task StartAsync()
    {
        if (_serverTask == null || _serverTask.IsCompleted)
        {
            _logger.LogInformation("Starting WebServer...");
            _serverTask = _app.RunAsync();
            _logger.LogInformation("WebServer run task started.");
        }
        else
        {
            _logger.LogInformation("WebServer already running.");
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        try
        {
            if (_serverTask != null && !_serverTask.IsCompleted)
            {
                _logger.LogInformation("Stopping WebServer...");
                await _app.StopAsync();
                await _serverTask;
            }
            else
            {
                _logger.LogInformation("WebServer not running; ensuring stop.");
                await _app.StopAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping WebServer.");
        }
        finally
        {
            _serverTask = null;
            _logger.LogInformation("WebServer stopped.");
        }
    }
}
