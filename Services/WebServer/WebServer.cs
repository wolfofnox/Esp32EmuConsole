using Esp32EmuConsole;

namespace Esp32EmuConsole.Services.WebServer;

class WebServer
{
    private readonly ILogger<WebServer> _logger;
    private readonly Services.Vite _vite;
    private readonly Services.Rules _rules;
    private Services.WebServer.Configuration _config;
    private WebApplication _app;
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

    public void Start()
    {
        ////// TODO Start in separate thread/task
        _logger.LogInformation("WebServer started.");
    }

    public void Stop()
    {
        _logger.LogInformation("WebServer stopped.");
    }
}
