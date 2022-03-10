// See https://aka.ms/new-console-template for more information

using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Connector.Tests.Containerization;
using AxonIQ.AxonServer.Grpc;
using AxonIQ.AxonServer.Grpc.Command;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

await using (var server = EmbeddedAxonServer.WithAccessControlDisabled(new NullLogger<EmbeddedAxonServer>()))
{
    await server.InitializeAsync();

    var httpEndpoint = server.GetHttpEndpoint();
    Console.WriteLine("Connect to {0}", new UriBuilder{ Host = httpEndpoint.Host, Port = httpEndpoint.Port }.Uri.AbsoluteUri);
    Console.ReadLine();
    
    var context = Context.Default;
    var component = new ComponentName("sample");
    var clientInstance1 = new ClientInstanceId("1");
    var clientInstance2 = new ClientInstanceId("1");

    var instance1 = new AxonServerConnectionFactory(AxonServerConnectionFactoryOptions.For(component, clientInstance1)
        .WithRoutingServers(server.GetGrpcEndpoint())
        .WithLoggerFactory(new NullLoggerFactory())
        .Build());
    var instance2 = new AxonServerConnectionFactory(AxonServerConnectionFactoryOptions.For(component, clientInstance2)
        .WithRoutingServers(server.GetGrpcEndpoint())
        .WithLoggerFactory(new NullLoggerFactory())
        .Build());

    var connection1 = await instance1.Connect(context);
    await connection1.WaitUntilConnected();
    
    var connection2 = await instance2.Connect(context);
    await connection2.WaitUntilConnected();

    var registration = 
        await connection1.CommandChannel.RegisterCommandHandler(
            (command, ct) => Task.FromResult(new CommandResponse
            {
                Payload = new SerializedObject
                {
                    Type = "pong",
                    Revision = "1",
                    Data = ByteString.CopyFromUtf8("{}")
                }
            }),
            new LoadFactor(1),
            new CommandName("ping"));

    await registration.WaitUntilCompleted();

    Console.WriteLine("Command handler registration completed");
    Console.ReadLine();

    var result = await connection2.CommandChannel.SendCommand(new Command
    {
        Name = "ping",
        Payload = new SerializedObject
        {
            Type = "ping",
            Revision = "1",
            Data = ByteString.CopyFromUtf8("{}")
        }
    }, CancellationToken.None);
    
    Console.ReadLine();
}