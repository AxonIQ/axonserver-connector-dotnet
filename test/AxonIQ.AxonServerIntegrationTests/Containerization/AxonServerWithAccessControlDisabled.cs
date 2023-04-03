using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.IntegrationTests.Containerization;

public class AxonServerWithAccessControlDisabled : Embedded.AxonServer, IAsyncLifetime
{
    public AxonServerWithAccessControlDisabled(IMessageSink sink)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        var logger = new MessageSinkLogger<EmbeddedAxonServer>(sink);
        logger.LogDebug("Using Embedded Axon Server with access control disabled");
        Server = EmbeddedAxonServer.WithAccessControlDisabled(logger);
    }

    protected override IAxonServer Server { get; }

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