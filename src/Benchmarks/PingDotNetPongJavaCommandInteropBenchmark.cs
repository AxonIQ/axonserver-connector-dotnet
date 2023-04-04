using System.Diagnostics;
using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Embedded;
using Benchmarks.Framework;
using Google.Protobuf;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Command;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

public class PingDotNetPongJavaCommandInteropBenchmark : IBenchmark
{
    private readonly int _commandCount;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PingDotNetPongJavaCommandInteropBenchmark> _logger;
    
    private IAxonServer _server;
    private AxonServerConnectionFactory _pingFactory;
    private IAxonServerConnection _ping;
    private Process _pong;

    public PingDotNetPongJavaCommandInteropBenchmark(int commandCount, ILoggerFactory loggerFactory)
    {
        _commandCount = commandCount;
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<PingDotNetPongJavaCommandInteropBenchmark>();
    }

    public string Name => $"{nameof(PingDotNetPongJavaCommandInteropBenchmark)}(CommandCount={_commandCount})";
    
    public async Task RunAsync()
    {
        var errorCode = ErrorCategory.NoHandlerForCommand;
        while(errorCode.Equals(ErrorCategory.NoHandlerForCommand))
        {
            var result = await _ping.CommandChannel.SendCommand(new Command
            {
                Name = "ping",
                Payload = new SerializedObject
                {
                    Type = "ping",
                    Revision = "1",
                    Data = ByteString.CopyFromUtf8($"ping={0}")
                }
            }, CancellationToken.None);
            errorCode = ErrorCategory.Parse(result.ErrorCode);
        }
        
        for(var command = 0; command < _commandCount; command++)
        {
            var result = await _ping.CommandChannel.SendCommand(new Command
            {
                Name = "ping",
                Payload = new SerializedObject
                {
                    Type = "ping",
                    Revision = "1",
                    Data = ByteString.CopyFromUtf8($"ping={command}")
                }
            }, CancellationToken.None);
            
            var pong = int.Parse(result.Payload.Data.ToStringUtf8().Substring("pong=".Length));
            if (pong != command)
            {
                throw new Exception($"Ping Pong Mismatch. Expected {command} but got {pong}");
            }

            _logger.LogDebug("Ping Pong Match for {command}", command);
        }
    }

    public async Task SetupAsync()
    {
        _server = EmbeddedAxonServer.WithAccessControlDisabled(_loggerFactory.CreateLogger<EmbeddedAxonServer>(), false);
        
        await _server.InitializeAsync();

        _pong = Process.Start(new ProcessStartInfo("java", $"-jar java/target/pong-1.0-SNAPSHOT-jar-with-dependencies.jar {_server.GetGrpcEndpoint().Host} {_server.GetGrpcEndpoint().Port}")
        {
            UseShellExecute = true
        });

        var context = Context.Default;
        var component = new ComponentName(nameof(PingPongCommandBenchmark));
        var clientInstance1 = new ClientInstanceId("dotnet-client");

        _pingFactory = new AxonServerConnectionFactory(AxonServerConnectionFactoryOptions
            .For(component, clientInstance1)
            .WithRoutingServers(_server.GetGrpcEndpoint())
            .WithLoggerFactory(_loggerFactory)
            .Build());
        
        _ping = await _pingFactory.ConnectAsync(context);
        await _ping.WaitUntilConnectedAsync();
    }

    public async Task TeardownAsync()
    {
        await _server.DisposeAsync();
        _pong.Kill(true);
        _pong.Dispose();
        // await _ping.DisposeAsync();
        // await _handler.DisposeAsync();
        // await _pong.DisposeAsync();
    }
}