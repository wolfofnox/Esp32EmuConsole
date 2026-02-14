namespace Esp32EmuConsole.Tests;

public class WebServerConfigurationTest
{
    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var config = new Services.WebServer.Configuration();

        // Assert
        Assert.Equal(5000, config.port);
        Assert.Equal(5173, config.vitePort);
        Assert.Equal("proxy.config.json", config.configFilePath);
    }

    [Fact]
    public void ViteUrl_IsFormattedCorrectly()
    {
        // Arrange
        var config = new Services.WebServer.Configuration();

        // Act & Assert
        Assert.Equal("http://localhost:5173", config.viteUrl);
    }

    [Fact]
    public void ListenUrl_IsFormattedCorrectly()
    {
        // Arrange
        var config = new Services.WebServer.Configuration();

        // Act & Assert
        Assert.Equal("http://localhost:5000", config.listenUrl);
    }

    [Fact]
    public void CustomPort_UpdatesUrls()
    {
        // Arrange
        var config = new Services.WebServer.Configuration
        {
            port = 8080,
            vitePort = 3000
        };

        // Act & Assert
        Assert.Equal("http://localhost:8080", config.listenUrl);
        Assert.Equal("http://localhost:3000", config.viteUrl);
    }

    [Fact]
    public void GetProxyCluster_ReturnsCorrectConfiguration()
    {
        // Arrange
        var config = new Services.WebServer.Configuration();

        // Act
        var clusters = config.GetProxyCluster;

        // Assert
        Assert.Single(clusters);
        Assert.Equal("vite", clusters[0].ClusterId);
        Assert.Single(clusters[0].Destinations);
        Assert.True(clusters[0].Destinations.ContainsKey("d1"));
        Assert.Equal("http://localhost:5173", clusters[0].Destinations["d1"].Address);
    }

    [Fact]
    public void GetProxyCluster_UsesCustomVitePort()
    {
        // Arrange
        var config = new Services.WebServer.Configuration
        {
            vitePort = 3000
        };

        // Act
        var clusters = config.GetProxyCluster;

        // Assert
        Assert.Equal("http://localhost:3000", clusters[0].Destinations["d1"].Address);
    }

    [Fact]
    public void GetProxyRoutes_ReturnsCorrectConfiguration()
    {
        // Arrange
        var config = new Services.WebServer.Configuration();

        // Act
        var routes = config.GetProxyRoutes;

        // Assert
        Assert.Single(routes);
        Assert.Equal("vite", routes[0].RouteId);
        Assert.Equal("vite", routes[0].ClusterId);
        Assert.NotNull(routes[0].Match);
        Assert.Equal("/{**catchall}", routes[0].Match.Path);
    }
}
