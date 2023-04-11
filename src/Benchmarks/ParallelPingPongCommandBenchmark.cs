using System.Collections.Concurrent;
using System.Text.Json;
using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Embedded;
using Benchmarks.Framework;
using Google.Protobuf;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Command;
using Microsoft.Extensions.Logging.Abstractions;

namespace Benchmarks;

public class ParallelPingPongCommandBenchmark : IBenchmark
{
    private readonly int _commandCount;
    private readonly int _maximumDegreeOfParallelism;

    private IAxonServer _server;
    private AxonServerConnectionFactory _pingFactory;
    private AxonServerConnectionFactory _pongFactory;
    private IAxonServerConnection _ping;
    private IAxonServerConnection _pong;
    private ICommandHandlerRegistration _handler;

    public ParallelPingPongCommandBenchmark(int commandCount, int maximumDegreeOfParallelism)
    {
        _commandCount = commandCount;
        _maximumDegreeOfParallelism = maximumDegreeOfParallelism;
    }

    public string Name => $"{nameof(ParallelPingPongCommandBenchmark)}(CommandCount={_commandCount},MaximumDegreeOfParallelism={_maximumDegreeOfParallelism})";
    
    public async Task RunAsync()
    {
        var commands = new List<(int, Task<CommandResponse>)>();
        var currentCount = _maximumDegreeOfParallelism;
        for(var command = 0; command < _commandCount; command++)
        {
            if (currentCount == 0)
            {
                // wait for all
                foreach (var (id, task) in commands)
                {
                    var result = await task.ConfigureAwait(false);
                    var pong = JsonSerializer.Deserialize<Pong>(result.Payload.Data.Span);
                    if (pong.Id != id)
                    {
                        throw new Exception($"Ping Pong Mismatch. Expected {command} but got {pong.Id}");
                    }    
                }
                commands.Clear();
                currentCount = _maximumDegreeOfParallelism;
            }
            commands.Add((command, _ping.CommandChannel.SendCommandAsync(new Command
            {
                Name = "ping",
                Payload = new SerializedObject
                {
                    Type = "ping",
                    Revision = "1",
                    Data = ByteString.CopyFromUtf8(JsonSerializer.Serialize(new Ping(command)))
                }
            }, CancellationToken.None)));
            currentCount--;
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

        _pingFactory = new AxonServerConnectionFactory(AxonServerConnectionFactoryOptions
            .For(component, clientInstance1)
            .WithRoutingServers(_server.GetGrpcEndpoint())
            .WithLoggerFactory(new NullLoggerFactory())
            .Build());
        _pongFactory = new AxonServerConnectionFactory(AxonServerConnectionFactoryOptions
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