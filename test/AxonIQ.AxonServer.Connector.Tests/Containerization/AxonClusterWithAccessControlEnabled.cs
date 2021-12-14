using AxonIQ.AxonServer.Connector.Tests.Framework;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class AxonClusterWithAccessControlEnabled : AxonCluster
{
    public AxonClusterWithAccessControlEnabled(IMessageSink logger)
    {
        if (logger == null) throw new ArgumentNullException(nameof(logger));
        logger.OnMessage(new DiagnosticMessage("Using Embedded Axon Server Container outside of CI"));
        Cluster = EmbeddedAxonCluster.WithAccessControlEnabled(new MessageSinkLogger<EmbeddedAxonCluster>(logger));
    }

    protected override IAxonCluster Cluster { get; }
}