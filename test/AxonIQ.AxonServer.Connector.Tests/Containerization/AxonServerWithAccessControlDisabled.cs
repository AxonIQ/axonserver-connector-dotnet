using AxonIQ.AxonServer.Connector.Tests.Framework;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class AxonServerWithAccessControlDisabled : AxonServer
{
    public AxonServerWithAccessControlDisabled(IMessageSink logger)
    {
        if (logger == null) throw new ArgumentNullException(nameof(logger));
        if (Environment.GetEnvironmentVariable("CI") != null)
        {
            logger.OnMessage(new DiagnosticMessage("Using Composed Axon Server Container inside of CI"));
            Server = ComposedAxonServer.WithAccessControlDisabled(logger);
        }
        else
        {
            logger.OnMessage(new DiagnosticMessage("Using Embedded Axon Server Container outside of CI"));
            Server = EmbeddedAxonServer.WithAccessControlDisabled(new MessageSinkLogger<EmbeddedAxonServer>(logger));
        }
    }

    protected override IAxonServer Server { get; }
}