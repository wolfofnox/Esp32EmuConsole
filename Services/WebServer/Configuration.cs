namespace Esp32EmuConsole.Services.WebServer;

public class Configuration
{
    public int port { get; set; } = 5000;
    public int vitePort { get; set; } = 5173;
    public string configFilePath { get; init; } = "proxy.config.json";
    public string viteUrl => $"http://localhost:{vitePort}";
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
