
using Esp32EmuConsole;
using Tui = Esp32EmuConsole.Tui;
using Services = Esp32EmuConsole.Services;
using Middleware = Esp32EmuConsole.Middleware;
using Utilities = Esp32EmuConsole.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Text.RegularExpressions;

var cwd = Environment.CurrentDirectory;
var vitePort = 5173;
var port = 5069;

var builder = WebApplication.CreateBuilder(args);

// Central in-memory log buffer and provider so TUI can show all logs.
// Create a single LogBuffer instance and configure logging to use only the
// in-memory provider so log messages (vite, proxy, etc.) do not get written
// to the console and overwrite the Spectre.Console UI.
var logBuffer = new Services.LogBuffer();
builder.Services.AddSingleton<Services.LogBuffer>(logBuffer);
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new Utilities.InMemoryLoggerProvider(logBuffer));

builder.Services.AddHostedService<Tui.HostedService>();

// Register Vite in DI but do not start the process in constructor
builder.Services.AddSingleton<Services.Vite>(sp => new Services.Vite(cwd, sp.GetRequiredService<ILogger<Services.Vite>>(), sp.GetRequiredService<ILoggerFactory>()));
builder.Services.AddSingleton<Services.Rules>(sp => new Services.Rules(cwd, sp.GetRequiredService<ILogger<Services.Rules>>()));
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

var app = builder.Build();

// Application startup message using the logging system (category: "app")
var _logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("app");
_logger.LogInformation("Starting Esp32EmuConsole on http://localhost:{Port}", port);

// Wire Console output into the LogBuffer so Console.WriteLine from Vite, middleware, etc gets captured
var origOut = Console.Out;
var origErr = Console.Error;
Console.SetOut(new Utilities.ConsoleForwarderTextWriter(origOut, logBuffer));
Console.SetError(new Utilities.ConsoleForwarderTextWriter(origErr, logBuffer, "[ERR] "));

app.UseWebSockets();

// Simple request logging
app.UseMiddleware<Middleware.ResponseLogger>();

// Use the static response middleware (short-circuits matching endpoints)
app.UseMiddleware<Middleware.StaticResponse>();

// Ensure config files and start Vite now that logging/providers are available
EnsureConfigFiles();
KillProcessUsingPort(vitePort);

// start the DI-registered Vite now that files are present and logging is available
var vite = app.Services.GetRequiredService<Services.Vite>();
vite.Start();

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

await app.RunAsync();

void EnsureConfigFiles()
{
    _logger.LogInformation("Ensuring config files exist in working directory: {WorkingDirectory}", cwd);
    foreach (var f in new[] {"vite.config.js", "package.json"})
    {
        var dest = Path.Combine(cwd, f);
        if (File.Exists(dest)) continue;

        var src = Path.Combine(AppContext.BaseDirectory, f);
        if (!File.Exists(src))
        {
            _logger.LogWarning("Template file not found: {Src}. Skipping copy for {File}.", src, f);
            continue;
        }

        try
        {
            File.Copy(src, dest);
            _logger.LogInformation("Copied {File} to working directory.", f);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy {File} from template.", f);
        }
    }
}
void KillProcessUsingPort(int port)
{
    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = "netstat",
            Arguments = "-ano",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi);
        if (p == null) return;
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();

        foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.Contains($":{port} ")) continue;
            var tokens = Regex.Split(line.Trim(), "\\s+");
            if (tokens.Length < 5) continue;
            if (!int.TryParse(tokens[^1], out var pid)) continue;
            if (pid == Process.GetCurrentProcess().Id) continue;
            if (pid == 0) continue;
            try
            {
                var victim = Process.GetProcessById(pid);
                victim.Kill(entireProcessTree: true);
                _logger.LogInformation("Killed process {Pid} listening on port {Port}.", pid, port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to kill process {Pid} on port {Port}.", pid, port);
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Port cleanup failed.");
    }
}