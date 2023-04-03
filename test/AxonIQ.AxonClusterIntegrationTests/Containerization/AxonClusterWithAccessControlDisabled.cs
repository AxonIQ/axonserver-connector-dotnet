using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonClusterIntegrationTests.Containerization;

public class AxonClusterWithAccessControlDisabled : AxonCluster, IAsyncLifetime
{
    public AxonClusterWithAccessControlDisabled(IMessageSink sink)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        var logger = new MessageSinkLogger<EmbeddedAxonCluster>(sink);
        logger.LogDebug("Using Embedded Axon Cluster with access control disabled");
        Cluster = EmbeddedAxonCluster.WithAccessControlDisabled(logger);
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