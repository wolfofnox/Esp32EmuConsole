
using Esp32EmuConsole;

var cwd = Environment.CurrentDirectory;
var vitePort = 5173;
var port = 5096;

var builder = WebApplication.CreateBuilder(args);

var initializer = new Initializer(cwd, AppContext.BaseDirectory);
initializer.EnsureConfigFiles();

builder.Services.AddSingleton<RuleService>(_ => new RuleService(cwd));
builder.Services.AddSingleton<ViteService>(_ => new ViteService(cwd));

// YARP reverse proxy configuration targeting Vite
builder.Services.AddReverseProxy().LoadFromMemory(
    routes: new[]
    {
        new Yarp.ReverseProxy.Configuration.RouteConfig
        {
            RouteId = "vite",
            ClusterId = "vite",
            Match = new Yarp.ReverseProxy.Configuration.RouteMatch { Path = "/{**catchall}" }
        }
    },
    clusters: new[]
    {
        new Yarp.ReverseProxy.Configuration.ClusterConfig
        {
            ClusterId = "vite",
            Destinations = new Dictionary<string, Yarp.ReverseProxy.Configuration.DestinationConfig>
            {
                { "d1", new Yarp.ReverseProxy.Configuration.DestinationConfig { Address = $"http://localhost:{vitePort}" } }
            }
        }
    }
);

builder.WebHost.UseUrls($"http://localhost:{port}");
Console.WriteLine($"Starting Esp32EmuConsole on http://localhost:{port}");

var app = builder.Build();

app.UseWebSockets();

// Simple request logging
app.UseMiddleware<ResponseLoggingMiddleware>();

// Use the static response middleware (short-circuits matching endpoints)
app.UseMiddleware<StaticResponseMiddleware>();

// Ensure port is free before starting Vite (defensive cleanup of stray processes)
initializer.KillProcessUsingPort(vitePort);
app.Services.GetRequiredService<ViteService>();

app.MapWs();

app.MapWhen(
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

app.Run();