namespace AxonIQ.AxonServer.Connector.Tests;

/// <summary>
/// Manages the interaction with a container composed in the CI environment.
/// </summary>
public class ComposedAxonServerContainer : IAxonServerContainer
{
    public ComposedAxonServerContainer()
    {
        if (Environment.GetEnvironmentVariable("AXONIQ_AXONSERVER_ADDRESS") == null)
        {
            throw new InvalidOperationException("The AXONIQ_AXONSERVER_ADDRESS environment variable is missing.");
        }
        if (Environment.GetEnvironmentVariable("AXONIQ_AXONSERVER_GRPC_ADDRESS") == null)
        {
            throw new InvalidOperationException("The AXONIQ_AXONSERVER_GRPC_ADDRESS environment variable is missing.");
        }
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}