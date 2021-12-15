using AxonIQ.AxonServer.Connector.Tests.Framework;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class AxonServerWithAccessControlDisabled : AxonServer
{
    public AxonServerWithAccessControlDisabled(IMessageSink sink)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        var logger = new MessageSinkLogger<EmbeddedAxonServer>(sink);
        logger.LogDebug("Using Embedded Axon Server Container outside of CI");
        Server = EmbeddedAxonServer.WithAccessControlDisabled(logger);
    }

    protected override IAxonServer Server { get; }
}