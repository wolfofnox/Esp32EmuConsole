using Esp32EmuConsole;

namespace Esp32EmuConsole.Services.WebServer;

class WebServer
{
    private readonly ILogger<WebServer> _logger;
    private readonly Services.Vite _vite;
    private readonly Services.Rules _rules;
    private readonly Services.WebServer.Configuration _config;
    private readonly WebApplication _app;
    private Task? _serverTask;

    public WebServer(WebApplication app)
    {
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _logger = _app.Services.GetRequiredService<ILogger<WebServer>>();
        _vite = _app.Services.GetRequiredService<Services.Vite>();
        _rules = _app.Services.GetRequiredService<Services.Rules>();
        _config = _app.Services.GetRequiredService<Services.WebServer.Configuration>();
    }
    public void Configure()
    { 
        _app.UseWebSockets();

        // Simple request logging
        _app.UseMiddleware<Middleware.ResponseLogger>();

        // Use the static response middleware (short-circuits matching endpoints)
        _app.UseMiddleware<Middleware.StaticResponse>();

        ////// TODO WebSocket???
        _app.MapWs();

        _app.MapWhen(
            ctx => !ctx.Request.Path.StartsWithSegments("/api") &&
                !ctx.Request.Path.StartsWithSegments("/ws"),
            branch =>
            {
                branch.UseRouting();
                branch.UseEndpoints(endpoints =>
                {
                    endpoints.MapReverseProxy();
                });
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
