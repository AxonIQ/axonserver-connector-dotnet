using Xunit.Abstractions;
using Xunit.Sdk;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class AxonServerContainerWithAccessControlEnabled : AxonServerContainer,
    IAxonServerContainerWithAccessControlEnabled
{
    public AxonServerContainerWithAccessControlEnabled(IMessageSink logger)
    {
        if (logger == null) throw new ArgumentNullException(nameof(logger));

        if (Environment.GetEnvironmentVariable("CI") != null)
        {
            logger.OnMessage(new DiagnosticMessage("Using Composed Axon Server Container inside of CI"));
            Container = ComposedAxonServerContainer.WithAccessControlEnabled(logger);
        }
        else
        {
            logger.OnMessage(new DiagnosticMessage("Using Embedded Axon Server Container outside of CI"));
            Container = EmbeddedAxonServerContainer.WithAccessControlEnabled(logger);
        }
    }

    protected override IAxonServerContainerWithAccessControlEnabled Container { get; }

    public string Token => Container.Token;
}