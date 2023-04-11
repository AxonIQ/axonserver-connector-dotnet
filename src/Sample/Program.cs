using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Embedded;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Command;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

Log.Logger = 
    new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
    .CreateLogger();

try
{
    var host =
        new HostBuilder()
            .UseSerilog()
            .Build();

    await using (var server = EmbeddedAxonServer.WithAccessControlDisabled(host.Services.GetRequiredService<ILogger<EmbeddedAxonServer>>()))
    {
        await server.InitializeAsync();

        var httpEndpoint = server.GetHttpEndpoint();
        Log.Information("Connect to {Host}",
            new UriBuilder { Host = httpEndpoint.Host, Port = httpEndpoint.Port }.Uri.AbsoluteUri);
        Console.ReadLine();

        var context = Context.Default;
        var component = new ComponentName("sample");
        var clientInstance1 = new ClientInstanceId("1");
        var clientInstance2 = new ClientInstanceId("2");

        var instance1 = new AxonServerConnectionFactory(AxonServerConnectionFactoryOptions
            .For(component, clientInstance1)
            .WithRoutingServers(server.GetGrpcEndpoint())
            .WithLoggerFactory(host.Services.GetRequiredService<ILoggerFactory>())
            .Build());
        var instance2 = new AxonServerConnectionFactory(AxonServerConnectionFactoryOptions
            .For(component, clientInstance2)
            .WithRoutingServers(server.GetGrpcEndpoint())
            .WithLoggerFactory(host.Services.GetRequiredService<ILoggerFactory>())
            .Build());

        var connection1 = await instance1.ConnectAsync(context);
        await connection1.WaitUntilConnectedAsync();

        var connection2 = await instance2.ConnectAsync(context);
        await connection2.WaitUntilConnectedAsync();

        var registration =
            await connection1.CommandChannel.RegisterCommandHandlerAsync(
                (command, ct) => Task.FromResult(new CommandResponse
                {
                    Payload = new SerializedObject
                    {
                        Type = "pong",
                        Revision = "1",
                        Data = ByteString.CopyFromUtf8("{}")
                    }
                }),
                new LoadFactor(100),
                new CommandName("ping"));

        await registration.WaitUntilCompletedAsync();

        Log.Information("Command handler registration completed");
        Console.ReadLine();

        var request1 = new Command
        {
            Name = "ping"
        };
        var result1 = await connection2.CommandChannel.SendCommandAsync(request1, CancellationToken.None);
        Log.Information(result1.ToString());
        
        var request2 = new Command
        {
            Name = "ping"
        };
        var result2 = await connection2.CommandChannel.SendCommandAsync(request2, CancellationToken.None);
        Log.Information(result2.ToString());

        Log.Information("Got response from sending command");
        Console.ReadLine();
    }
}
catch (Exception exception)
{
    Log.Fatal(exception, "The host terminated unexpectedly because of an exception");
}
finally
{
    Log.CloseAndFlush();
}