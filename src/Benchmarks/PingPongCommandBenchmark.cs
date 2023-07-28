using System.Text.Json;
using System.Text.Json.Serialization;
using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Embedded;
using Benchmarks.Framework;
using Google.Protobuf;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Command;
using Microsoft.Extensions.Logging.Abstractions;

namespace Benchmarks;

public class PingPongCommandBenchmark : IBenchmark
{
    private readonly int _commandCount;
    
    private IAxonServer _server;
    private AxonServerConnectionFactory _pingFactory;
    private AxonServerConnectionFactory _pongFactory;
    private IAxonServerConnection _ping;
    private IAxonServerConnection _pong;
    private ICommandHandlerRegistration _handler;

    public PingPongCommandBenchmark(int commandCount)
    {
        _commandCount = commandCount;
    }

    public string Name => $"{nameof(PingPongCommandBenchmark)}(CommandCount={_commandCount})";
    
    public async Task RunAsync()
    {
        for(var command = 0; command < _commandCount; command++)
        {
            var result = await _ping.CommandChannel.SendCommandAsync(new Command
            {
                Name = "ping",
                Payload = new SerializedObject
                {
                    Type = "ping",
                    Revision = "1",
                    Data = ByteString.CopyFromUtf8(JsonSerializer.Serialize(new Ping(command)))
                }
            }, CancellationToken.None);

            var pong = JsonSerializer.Deserialize<Pong>(result.Payload.Data.Span);
            if (pong.Id != command)
            {
                throw new Exception($"Ping Pong Mismatch. Expected {command} but got {pong.Id}");
            }
        }
    }

    public async Task SetupAsync()
    {
        _server = EmbeddedAxonServer.WithAccessControlDisabled(new NullLogger<EmbeddedAxonServer>());
        await _server.InitializeAsync();
        
        var context = Context.Default;
        var component = new ComponentName(nameof(PingPongCommandBenchmark));
        var clientInstance1 = new ClientInstanceId("1");
        var clientInstance2 = new ClientInstanceId("2");

        _pingFactory = new AxonServerConnectionFactory(AxonServerConnectorOptions
            .For(component, clientInstance1)
            .WithRoutingServers(_server.GetGrpcEndpoint())
            .WithLoggerFactory(new NullLoggerFactory())
            .Build());
        _pongFactory = new AxonServerConnectionFactory(AxonServerConnectorOptions
            .For(component, clientInstance2)
            .WithRoutingServers(_server.GetGrpcEndpoint())
            .WithLoggerFactory(new NullLoggerFactory())
            .Build());
        
        _ping = await _pingFactory.ConnectAsync(context);
        await _ping.WaitUntilConnectedAsync();

        _pong = await _pongFactory.ConnectAsync(context);
        await _pong.WaitUntilConnectedAsync();
        
        _handler =
            await _pong.CommandChannel.RegisterCommandHandlerAsync(
                (command, ct) =>
                {
                    var ping = JsonSerializer.Deserialize<Ping>(command.Payload.Data.Span);
                    return Task.FromResult(new CommandResponse
                    {
                        Payload = new SerializedObject
                        {
                            Type = "pong",
                            Revision = "1",
                            Data = ByteString.CopyFromUtf8(JsonSerializer.Serialize(new Pong(ping.Id)))
                        }
                    });
                },
                new LoadFactor(100),
                new CommandName("ping"));

        await _handler.WaitUntilCompletedAsync();
    }

    public async Task TeardownAsync()
    {
        await _server.DisposeAsync();
        // await _ping.DisposeAsync();
        // await _handler.DisposeAsync();
        // await _pong.DisposeAsync();
    }

    private record Ping(int Id);

    private record Pong(int Id);
}