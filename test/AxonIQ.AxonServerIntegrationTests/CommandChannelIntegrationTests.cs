using System.Net;
using AutoFixture;
using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Connector.Tests;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using AxonIQ.AxonServerIntegrationTests.Containerization;
using Google.Protobuf;
using Grpc.Net.Client;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Command;
using Io.Axoniq.Axonserver.Grpc.Control;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServerIntegrationTests;

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
        return factory.ConnectAsync(Context.Default);
    }

    [Fact]
    public async Task RegisterCommandHandlerHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        await connection.WaitUntilReadyAsync();
        
        var sut = connection.CommandChannel;

        var requestId = InstructionId.New();
        var responseId = InstructionId.New();
        var commandName = _fixture.Create<CommandName>();
        var registration = await sut.RegisterCommandHandlerAsync((command, ct) => Task.FromResult(new CommandResponse
        {
            MessageIdentifier = responseId.ToString(),
            Payload = new SerializedObject
            {
                Type = "pong",
                Revision = "0",
                Data = ByteString.CopyFromUtf8("{ \"pong\": true }")
            }
            
        }), new LoadFactor(1), commandName);

        await registration.WaitUntilCompletedAsync();

        var result = await sut.SendCommandAsync(new Command
        {
            Name = commandName.ToString(),
            MessageIdentifier = requestId.ToString()
        }, CancellationToken.None);
                
        Assert.Equal(responseId.ToString(), result.MessageIdentifier);
        Assert.Equal(requestId.ToString(), result.RequestIdentifier);
    }
    
    [Fact]
    public async Task RegisterCommandHandlerForAlreadyRegisteredCommandNameHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        await connection.WaitUntilReadyAsync();
        
        var sut = connection.CommandChannel;

        var requestId = InstructionId.New();
        var responseId1 = InstructionId.New();
        var responseId2 = InstructionId.New();
        var commandName = _fixture.Create<CommandName>();
        var registration1 = await sut.RegisterCommandHandlerAsync((command, ct) => Task.FromResult(new CommandResponse
        {
            MessageIdentifier = responseId1.ToString(),
            Payload = new SerializedObject
            {
                Type = "pong",
                Revision = "0",
                Data = ByteString.CopyFromUtf8("{ \"pong\": 1 }")
            }
            
        }), new LoadFactor(1), commandName);
        await registration1.WaitUntilCompletedAsync();
        
        var registration2 = await sut.RegisterCommandHandlerAsync((command, ct) => Task.FromResult(new CommandResponse
        {
            MessageIdentifier = responseId2.ToString(),
            Payload = new SerializedObject
            {
                Type = "pong",
                Revision = "0",
                Data = ByteString.CopyFromUtf8("{ \"pong\": 2 }")
            }
            
        }), new LoadFactor(1), commandName);
        await registration2.WaitUntilCompletedAsync();
        
        var result = await sut.SendCommandAsync(new Command
        {
            Name = commandName.ToString(),
            MessageIdentifier = requestId.ToString()
        }, CancellationToken.None);
                
        Assert.Equal(responseId2.ToString(), result.MessageIdentifier);
        Assert.Equal(requestId.ToString(), result.RequestIdentifier);
    }
    
    [Fact]
    public async Task UnregisterCommandHandlerHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        await connection.WaitUntilConnectedAsync();
        
        var sut = connection.CommandChannel;

        var requestId = InstructionId.New();
        var responseId = InstructionId.New();
        var commandName = _fixture.Create<CommandName>();
        var registration = await sut.RegisterCommandHandlerAsync((command, ct) => Task.FromResult(new CommandResponse
        {
            MessageIdentifier = responseId.ToString(),
            Payload = new SerializedObject
            {
                Type = "pong",
                Revision = "0",
                Data = ByteString.CopyFromUtf8("{ \"pong\": true }")
            }
            
        }), new LoadFactor(1), commandName);

        await registration.WaitUntilCompletedAsync();

        var result = await sut.SendCommandAsync(new Command
        {
            Name = commandName.ToString(),
            MessageIdentifier = requestId.ToString()
        }, CancellationToken.None);
                
        Assert.Equal(responseId.ToString(), result.MessageIdentifier);
        Assert.Equal(requestId.ToString(), result.RequestIdentifier);

        await registration.DisposeAsync();
        
        var response = await sut.SendCommandAsync(new Command
        {
            Name = commandName.ToString(),
            MessageIdentifier = requestId.ToString()
        }, CancellationToken.None);
        
        Assert.Equal(response.ErrorCode, ErrorCategory.NoHandlerForCommand.ToString());
    }
    
    // [Fact(Skip = "This needs work")]
    // public async Task ReconnectsAfterConnectionFailure()
    // {
    //     var server = await CreateSystemUnderTest(
    //         configure => 
    //         configure.WithRoutingServers(_container.GetGrpcProxyEndpoint()));
    //     await server.WaitUntilReadyAsync();
    //
    //     var client = await CreateSystemUnderTest();
    //     await client.WaitUntilReadyAsync();
    //     
    //     var requestId = InstructionId.New();
    //     var responseId = InstructionId.New();
    //     var commandName = _fixture.Create<CommandName>();
    //     var registration = await server.CommandChannel.RegisterCommandHandlerAsync((command, ct) => Task.FromResult(new CommandResponse
    //     {
    //         MessageIdentifier = responseId.ToString(),
    //         Payload = new SerializedObject
    //         {
    //             Type = "pong",
    //             Revision = "0",
    //             Data = ByteString.CopyFromUtf8("{ \"pong\": true }")
    //         }
    //         
    //     }), new LoadFactor(1), commandName);
    //
    //     await registration.WaitUntilCompletedAsync();
    //
    //     await _container.DisableGrpcProxyEndpointAsync();
    //
    //     await Task.Delay(TimeSpan.FromSeconds(2));
    //
    //     await _container.EnableGrpcProxyEndpointAsync();
    //     
    //     await Task.Delay(TimeSpan.FromSeconds(2));
    //
    //     var result = await client.CommandChannel.SendCommandAsync(new Command
    //     {
    //         Name = commandName.ToString(),
    //         MessageIdentifier = requestId.ToString()
    //     }, CancellationToken.None);
    //
    //     Assert.Null(result.ErrorCode);
    //     Assert.Equal(responseId.ToString(), result.MessageIdentifier);
    //     Assert.Equal(requestId.ToString(), result.RequestIdentifier);
    // }
}