using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class ToxicAxonServerWithAccessControlDisabled : ToxicAxonServer, IAsyncLifetime
{
    public ToxicAxonServerWithAccessControlDisabled(IMessageSink sink)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        var server = EmbeddedAxonServer.WithAccessControlDisabled(new MessageSinkLogger<EmbeddedAxonServer>(sink));
        var logger = new MessageSinkLogger<EmbeddedToxicAxonServer>(sink);
        logger.LogDebug("Using Embedded Toxic Axon Server with access control disabled");
        Server = new EmbeddedToxicAxonServer(server, logger);
    }

    protected override IToxicAxonServer Server { get; }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await Server.InitializeAsync();
        await Server.WaitUntilAvailableAsync();
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        return Server.DisposeAsync();
    }
}