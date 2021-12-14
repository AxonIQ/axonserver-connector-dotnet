using AxonIQ.AxonServer.Connector.Tests.Framework;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class AxonServerWithAccessControlEnabled : AxonServer
{
    public AxonServerWithAccessControlEnabled(IMessageSink logger)
    {
        if (logger == null) throw new ArgumentNullException(nameof(logger));

        logger.OnMessage(new DiagnosticMessage("Using Embedded Axon Server Container outside of CI"));
        Server = EmbeddedAxonServer.WithAccessControlEnabled(new MessageSinkLogger<EmbeddedAxonServer>(logger));
    }

    protected override IAxonServer Server { get; }
}