using Xunit.Abstractions;
using Xunit.Sdk;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonServerContainer : IAxonServerContainer
{
    private readonly IAxonServerContainer _container;
    
    public AxonServerContainer(IMessageSink logger)
    {
        if (logger == null) throw new ArgumentNullException(nameof(logger));
        if (Environment.GetEnvironmentVariable("CI") != null)
        {
            logger.OnMessage(new DiagnosticMessage("Using Composed Axon Server Container inside of CI"));
            _container = new ComposedAxonServerContainer(logger);
        }
        else
        {
            logger.OnMessage(new DiagnosticMessage("Using Embedded Axon Server Container outside of CI"));
            _container = new EmbeddedAxonServerContainer(logger);
        }
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