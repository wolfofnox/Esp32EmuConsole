
using Tui = Esp32EmuConsole.Tui;
using Services = Esp32EmuConsole.Services;
using Utilities = Esp32EmuConsole.Utilities;
using System.Diagnostics;
using System.Text.RegularExpressions;

var webConfig = new Services.WebServer.Configuration();
var tuiConfig = new Tui.Configuration();

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// Central in-memory log buffer and provider so TUI can show all logs.
var appBuf = new Utilities.LogBuffer();
var httpBuf = new Utilities.LogBuffer();
var wsBuf = new Utilities.LogBuffer();
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new Utilities.InMemoryLoggerProvider(new[]
{
    new Utilities.LogRoute("fallback", LogLevel.Trace, Utilities.LogFormat.Full, appBuf),
    new Utilities.LogRoute("Yarp.ReverseProxy.*", LogLevel.Warning, Utilities.LogFormat.Full, appBuf),
    new Utilities.LogRoute("Http*,HTTP*,http*", LogLevel.Trace, Utilities.LogFormat.OmitCategory, httpBuf),
    new Utilities.LogRoute("WS*,Ws*,ws*,WebSocket*,websocket*", LogLevel.Trace, Utilities.LogFormat.OmitCategory, wsBuf),
}));

// Register configs
builder.Services.AddSingleton(webConfig);
builder.Services.AddSingleton(tuiConfig);

// Register LogBuffers with keys so TUI can access them
builder.Services.AddKeyedSingleton("AppLogBuffer", appBuf);
builder.Services.AddKeyedSingleton("HttpLogBuffer", httpBuf);
builder.Services.AddKeyedSingleton("WsLogBuffer", wsBuf);
// Register Vite in DI but do not start the process in constructor
var cwd = Environment.CurrentDirectory;
builder.Services.AddSingleton(sp => new Services.Vite(cwd, sp.GetRequiredService<ILogger<Services.Vite>>(), sp.GetRequiredService<ILoggerFactory>()));
builder.Services.AddSingleton(sp => new Services.Rules(cwd, sp.GetRequiredService<ILogger<Services.Rules>>()));
// YARP reverse proxy configuration targeting Vite
builder.Services.AddReverseProxy().LoadFromMemory(
    routes: webConfig.GetProxyRoutes,
    clusters: webConfig.GetProxyCluster
);

builder.WebHost.UseUrls(webConfig.listenUrl);

var app = builder.Build();

// Application startup message using the logging system (category: "app")
var _logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("app");
_logger.LogInformation($"Starting Esp32EmuConsole on {webConfig.listenUrl}");

// Ensure config files and start Vite now that logging/providers are available
EnsureConfigFiles();
KillProcessUsingPort(webConfig.vitePort);

// start the DI-registered Vite now that files are present and logging is available
var vite = app.Services.GetRequiredService<Services.Vite>();
vite.Start();
await vite.WaitForViteAsync(TimeSpan.FromSeconds(20));

var webServer = new Services.WebServer.WebServer(app);
webServer.Configure();
await webServer.StartAsync();

var tui = new Tui.TUI(app.Services);
tui.Run();

//////// TODO move to master config class
void EnsureConfigFiles()
{
    _logger.LogInformation("Ensuring config files exist in working directory: {WorkingDirectory}", cwd);
    
    var templateDir = Path.Combine(AppContext.BaseDirectory, "templates");
    if (!Directory.Exists(templateDir))
    {
        throw new DirectoryNotFoundException($"Template directory not found: {templateDir}");
    }

    foreach (var f in new[] {"vite.config.js", "package.json"})
    {
        var dest = Path.Combine(cwd, f);
        if (File.Exists(dest)) continue;

        var src = Path.Combine(templateDir, f);
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