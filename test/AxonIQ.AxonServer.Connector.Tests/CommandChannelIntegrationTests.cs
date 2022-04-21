using System.Net;
using AutoFixture;
using AxonIQ.AxonServer.Connector.Tests.Containerization;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Grpc;
using AxonIQ.AxonServer.Grpc.Command;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
public class CommandChannelIntegrationTests
{
    private readonly IAxonServer _container;
    private readonly Fixture _fixture;
    private readonly ILoggerFactory _loggerFactory;

    public CommandChannelIntegrationTests(AxonServerWithAccessControlDisabled container, ITestOutputHelper output)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _fixture = new Fixture();
        _fixture.CustomizeClientInstanceId();
        _fixture.CustomizeComponentName();
        _loggerFactory = new TestOutputHelperLoggerFactory(output);
    }
    
    private Task<IAxonServerConnection> CreateSystemUnderTest(
        Action<IAxonServerConnectionFactoryOptionsBuilder>? configure = default)
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientInstanceId>();

        var builder = AxonServerConnectionFactoryOptions.For(component, clientInstance)
            .WithRoutingServers(_container.GetGrpcEndpoint())
            .WithLoggerFactory(_loggerFactory);
        configure?.Invoke(builder);
        var options = builder.Build();
        var factory = new AxonServerConnectionFactory(options);
        return factory.Connect(Context.Default);
    }

    [Fact]
    public async Task RegisterCommandHandlerWhileDisconnectedHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest(builder =>
            builder.WithRoutingServers(new DnsEndPoint("127.0.0.0", AxonServerConnectionFactoryDefaults.Port)));
        
        var sut = connection.CommandChannel;

        var commandName = _fixture.Create<CommandName>();
        var registration = await sut.RegisterCommandHandler(
            (_, _) => Task.FromResult(new CommandResponse()),
            new LoadFactor(10), commandName);

        await Assert.ThrowsAsync<AxonServerException>(() => registration.WaitUntilCompleted());
    }
 
    [Fact]
    public async Task RegisterCommandHandlerWhileConnectedHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        await connection.WaitUntilConnected();
        
        var sut = connection.CommandChannel;

        var requestId = InstructionId.New();
        var responseId = InstructionId.New();
        var commandName = _fixture.Create<CommandName>();
        var registration = await sut.RegisterCommandHandler((command, ct) => Task.FromResult(new CommandResponse
        {
            MessageIdentifier = responseId.ToString(),
            Payload = new SerializedObject
            {
                Type = "pong",
                Revision = "0",
                Data = ByteString.CopyFromUtf8("{ \"pong\": true }")
            }
            
        }), new LoadFactor(1), commandName);

        await registration.WaitUntilCompleted();

        var result = await sut.SendCommand(new Command
        {
            Name = commandName.ToString(),
            MessageIdentifier = requestId.ToString()
        }, CancellationToken.None);
                
        Assert.Equal(responseId.ToString(), result.MessageIdentifier);
        Assert.Equal(requestId.ToString(), result.RequestIdentifier);
    }
}