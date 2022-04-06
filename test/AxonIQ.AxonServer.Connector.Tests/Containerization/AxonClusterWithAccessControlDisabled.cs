using AxonIQ.AxonServer.Connector.Tests.Framework;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class AxonClusterWithAccessControlDisabled : AxonCluster
{
    public AxonClusterWithAccessControlDisabled(IMessageSink sink)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        var logger = new MessageSinkLogger<EmbeddedAxonCluster>(sink);
        logger.LogDebug("Using Embedded Axon Cluster with access control disabled");
        Cluster = EmbeddedAxonCluster.WithAccessControlDisabled(logger);
    }

    protected override IAxonCluster Cluster { get; }
}