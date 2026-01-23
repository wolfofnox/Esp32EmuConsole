using Microsoft.AspNetCore.Builder;
using Yarp.ReverseProxy;
using Esp32EmuConsole;

var builder = WebApplication.CreateBuilder(args);

var cwd = Environment.CurrentDirectory;
var configInitializer = new ConfigInitializer(cwd, AppContext.BaseDirectory);
configInitializer.EnsureFiles();
builder.Services.AddSingleton<RuleService>(_ => new RuleService(cwd));
builder.Services.AddSingleton<ViteService>(_ => new ViteService(cwd));

// YARP reverse proxy configuration targeting Vite
var viteUrl = "http://localhost:5173";
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
                { "d1", new Yarp.ReverseProxy.Configuration.DestinationConfig { Address = viteUrl } }
            }
        }
    }
);

builder.WebHost.UseUrls("http://localhost:5069");
Console.WriteLine("Starting Esp32EmuConsole on http://localhost:5069");

var app = builder.Build();

app.UseWebSockets();

// Simple request logging
app.UseMiddleware<ResponseLoggingMiddleware>();

// Use the static response middleware (short-circuits matching endpoints)
app.UseMiddleware<StaticResponseMiddleware>();

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