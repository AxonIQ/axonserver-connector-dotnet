namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonServerContainer : IAxonServerContainer
{
    private readonly IAxonServerContainer _container;
    
    public AxonServerContainer()
    {
        _container = Environment.GetEnvironmentVariable("CI") != null
            ? new ComposedAxonServerContainer()
            : new EmbeddedAxonServerContainer();
    }
    public Task InitializeAsync()
    {
        return _container.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        return _container.DisposeAsync();
    }
}