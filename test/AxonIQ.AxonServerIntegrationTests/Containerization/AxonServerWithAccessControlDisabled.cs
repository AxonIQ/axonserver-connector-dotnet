using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServerIntegrationTests.Containerization;

public class AxonServerWithAccessControlDisabled : AxonServer.Embedded.AxonServer, IAsyncLifetime
{
    public AxonServerWithAccessControlDisabled(IMessageSink sink)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        var logger = new MessageSinkLogger<EmbeddedAxonServer>(sink);
        logger.LogDebug("Using Embedded Axon Server with access control disabled");
        Server = EmbeddedAxonServer.WithAccessControlDisabled(logger);
    }
    
    public AxonServerWithAccessControlDisabled(ITestOutputHelper output)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        var logger = new TestOutputHelperLogger<EmbeddedAxonServer>(output);
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