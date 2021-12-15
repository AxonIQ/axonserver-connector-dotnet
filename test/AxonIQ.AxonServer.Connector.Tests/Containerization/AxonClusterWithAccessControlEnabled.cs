using AxonIQ.AxonServer.Connector.Tests.Framework;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class AxonClusterWithAccessControlEnabled : AxonCluster
{
    public AxonClusterWithAccessControlEnabled(IMessageSink sink)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        var logger = new MessageSinkLogger<EmbeddedAxonCluster>(sink);
        logger.LogDebug("Using Embedded Axon Cluster with access control enabled");
        Cluster = EmbeddedAxonCluster.WithAccessControlEnabled(logger);
    }

    protected override IAxonCluster Cluster { get; }
}