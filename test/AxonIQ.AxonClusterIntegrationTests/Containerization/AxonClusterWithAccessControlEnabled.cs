using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonClusterIntegrationTests.Containerization;

public class AxonClusterWithAccessControlEnabled : AxonCluster, IAsyncLifetime
{
    public AxonClusterWithAccessControlEnabled(IMessageSink sink)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        var logger = new MessageSinkLogger<EmbeddedAxonCluster>(sink);
        logger.LogDebug("Using Embedded Axon Cluster with access control enabled");
        Cluster = EmbeddedAxonCluster.WithAccessControlEnabled(logger);
    }

    protected override IAxonCluster Cluster { get; }
    
    async Task IAsyncLifetime.InitializeAsync()
    {
        await Cluster.InitializeAsync();
        await Cluster.WaitUntilAvailableAsync();
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        return Cluster.DisposeAsync();
    }
}