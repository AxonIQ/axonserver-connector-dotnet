using Xunit.Abstractions;
using Xunit.Sdk;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class AxonServerContainerWithAccessControlDisabled : AxonServerContainer,
    IAxonServerContainerWithAccessControlDisabled
{
    public AxonServerContainerWithAccessControlDisabled(IMessageSink logger)
    {
        if (logger == null) throw new ArgumentNullException(nameof(logger));
        if (Environment.GetEnvironmentVariable("CI") != null)
        {
            logger.OnMessage(new DiagnosticMessage("Using Composed Axon Server Container inside of CI"));
            Container = ComposedAxonServerContainer.WithAccessControlDisabled(logger);
        }
        else
        {
            logger.OnMessage(new DiagnosticMessage("Using Embedded Axon Server Container outside of CI"));
            Container = EmbeddedAxonServerContainer.WithAccessControlDisabled(logger);
        }
    }

    protected override IAxonServerContainer Container { get; }
}