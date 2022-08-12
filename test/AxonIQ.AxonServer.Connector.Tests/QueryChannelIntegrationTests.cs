/*
 * Copyright (c) 2022. AxonIQ
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Linq;
using System.Net;
using AutoFixture;
using AxonIQ.AxonServer.Connector.Tests.Containerization;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using Google.Protobuf;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
public class QueryChannelIntegrationTests
{
    private readonly IAxonServer _container;
    private readonly Fixture _fixture;
    private readonly ILoggerFactory _loggerFactory;

    public QueryChannelIntegrationTests(AxonServerWithAccessControlDisabled container, ITestOutputHelper output)
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
    public async Task RegisterQueryHandlerWhileDisconnectedHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest(builder =>
            builder.WithRoutingServers(new DnsEndPoint("127.0.0.0", AxonServerConnectionFactoryDefaults.Port)));
        
        var sut = connection.QueryChannel;

        var queries = new[]
        {
            new QueryDefinition(new QueryName("Ping"), "Pong")
        };
        var registration = await sut.RegisterQueryHandler(
            new QueryHandler(),
            queries);

        await Assert.ThrowsAsync<AxonServerException>(() => registration.WaitUntilCompleted());
    }
 
    [Fact]
    public async Task RegisterQueryHandlerWhileConnectedHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        await connection.WaitUntilConnected();
        
        var sut = connection.QueryChannel;
    
        var requestId = InstructionId.New();
        var responseId = InstructionId.New();
        var queries = new[]
        {
            new QueryDefinition(new QueryName("Ping"), "Pong")
        };
        var registration = await sut.RegisterQueryHandler(
            new PingPongQueryHandler(responseId), 
            queries);
    
        await registration.WaitUntilCompleted();
    
        var result = sut.Query(new QueryRequest
        {
            Query = "Ping",
            MessageIdentifier = requestId.ToString()
        }, CancellationToken.None);

        var actual = await result.ToArrayAsync();
        var response = Assert.Single(actual);
        Assert.Equal(responseId.ToString(), response.MessageIdentifier);
        Assert.Equal(requestId.ToString(), response.RequestIdentifier);
        Assert.Equal("pong", response.Payload.Type);
        Assert.Equal("0", response.Payload.Revision);
        Assert.Equal(ByteString.CopyFromUtf8("{ \"pong\": true }").ToByteArray(), response.Payload.Data.ToByteArray());
    }

    //
    // [Fact]
    // public async Task UnregisterCommandHandlerHasExpectedResult()
    // {
    //     var connection = await CreateSystemUnderTest();
    //     await connection.WaitUntilConnected();
    //     
    //     var sut = connection.CommandChannel;
    //
    //     var requestId = InstructionId.New();
    //     var responseId = InstructionId.New();
    //     var commandName = _fixture.Create<CommandName>();
    //     var registration = await sut.RegisterCommandHandler((command, ct) => Task.FromResult(new CommandResponse
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
    //     await registration.WaitUntilCompleted();
    //
    //     var result = await sut.SendCommand(new Command
    //     {
    //         Name = commandName.ToString(),
    //         MessageIdentifier = requestId.ToString()
    //     }, CancellationToken.None);
    //             
    //     Assert.Equal(responseId.ToString(), result.MessageIdentifier);
    //     Assert.Equal(requestId.ToString(), result.RequestIdentifier);
    //
    //     await registration.DisposeAsync();
    //     
    //     var response = await sut.SendCommand(new Command
    //     {
    //         Name = commandName.ToString(),
    //         MessageIdentifier = requestId.ToString()
    //     }, CancellationToken.None);
    //     
    //     Assert.Equal(response.ErrorCode, ErrorCategory.NoHandlerForCommand.ToString());
    // }
    //
    private class QueryHandler : IQueryHandler
    {
        public Task Handle(QueryRequest request, IQueryResponseChannel responseChannel)
        {
            return Task.CompletedTask;
        }
    }
    
    private class PingPongQueryHandler : IQueryHandler
    {
        private readonly InstructionId _responseId;

        public PingPongQueryHandler(InstructionId responseId)
        {
            _responseId = responseId;
        }
        
        public async Task Handle(QueryRequest request, IQueryResponseChannel responseChannel)
        {
            if (request.Query == "Ping")
            {
                await responseChannel.WriteAsync(new QueryResponse
                {
                    MessageIdentifier = _responseId.ToString(),
                    RequestIdentifier = request.MessageIdentifier,
                    Payload = new SerializedObject
                    {
                        Type = "pong",
                        Revision = "0",
                        Data = ByteString.CopyFromUtf8("{ \"pong\": true }")
                    }
                });
                await responseChannel.CompleteAsync();
            }
        }
    }
}