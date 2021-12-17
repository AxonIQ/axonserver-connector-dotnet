using AxonIQ.AxonServer.Connector.Tests.Framework;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class AxonServerWithAccessControlEnabled : AxonServer
{
    public AxonServerWithAccessControlEnabled(IMessageSink sink)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        var logger = new MessageSinkLogger<EmbeddedAxonServer>(sink);
        logger.LogDebug("Using Embedded Axon Server with access control enabled");
        Server = EmbeddedAxonServer.WithAccessControlEnabled(logger);
    }

    protected override IAxonServer Server { get; }
}