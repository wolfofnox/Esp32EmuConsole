namespace Esp32EmuConsole.Services.WebServer;

/// <summary>
/// Holds runtime configuration values for the embedded HTTP server and the YARP
/// reverse-proxy routes that forward unmatched requests to the Vite dev server.
/// </summary>
public class Configuration
{
    /// <summary>Port the mock HTTP server listens on. Default: <c>5000</c>.</summary>
    public int port { get; set; } = 5000;

    /// <summary>Port the embedded Vite dev server runs on. Default: <c>5173</c>.</summary>
    public int vitePort { get; set; } = 5173;

    /// <summary>Path to the proxy configuration file (currently unused at runtime).</summary>
    public string configFilePath { get; init; } = "proxy.config.json";

    /// <summary>Base URL of the Vite dev server, derived from <see cref="vitePort"/>.</summary>
    public string viteUrl => $"http://localhost:{vitePort}";

    /// <summary>URL the mock server listens on, derived from <see cref="port"/>.</summary>
    public string listenUrl => $"http://localhost:{port}";
    public Yarp.ReverseProxy.Configuration.ClusterConfig[] GetProxyCluster => new[]
    {
        new Yarp.ReverseProxy.Configuration.ClusterConfig
        {
            ClusterId = "vite",
            Destinations = new Dictionary<string, Yarp.ReverseProxy.Configuration.DestinationConfig>
            {
                { "d1", new Yarp.ReverseProxy.Configuration.DestinationConfig { Address = $"http://localhost:{vitePort}" } }
            }
        }
    };
    public Yarp.ReverseProxy.Configuration.RouteConfig[] GetProxyRoutes => new[]
    {
        new Yarp.ReverseProxy.Configuration.RouteConfig
        {
            RouteId = "vite",
            ClusterId = "vite",
            Match = new Yarp.ReverseProxy.Configuration.RouteMatch { Path = "/{**catchall}" }
        }
    };
}
