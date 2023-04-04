using AutoFixture;
using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Connector.Tests;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using AxonIQ.AxonServerIntegrationTests.Containerization;
using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc.Admin;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServerIntegrationTests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
public class AxonServerAdminChannelIntegrationTests
{
    private readonly IAxonServer _container;
    private readonly Fixture _fixture;
    private readonly ILoggerFactory _loggerFactory;

    public AxonServerAdminChannelIntegrationTests(AxonServerWithAccessControlDisabled container, ITestOutputHelper output)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _fixture = new Fixture();
        _fixture.CustomizeClientInstanceId();
        _fixture.CustomizeComponentName();
        _fixture.CustomizeEventProcessorName();
        _fixture.CustomizeSegmentId();
        _loggerFactory = new TestOutputHelperLoggerFactory(output);
    }

    private async Task<IAxonServerConnection> CreateSystemUnderTest(
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
        var connection = await factory.ConnectAsync(Context.Default);
        await connection.WaitUntilReadyAsync();
        return connection;
    }
    
    // Event Processors
    
    [Fact]
    public async Task StartNonExistingEventProcessorHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var name = _fixture.Create<EventProcessorName>();
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.StartEventProcessor(name, TokenStoreIdentifier.Empty));
        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }
    
    [Fact]
    public async Task PauseNonExistingEventProcessorHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var name = _fixture.Create<EventProcessorName>();
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.PauseEventProcessor(name, TokenStoreIdentifier.Empty));
        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }
    
    [Fact]
    public async Task SplitNonExistingEventProcessorHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var name = _fixture.Create<EventProcessorName>();
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.SplitEventProcessor(name, TokenStoreIdentifier.Empty));
        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }
    
    [Fact]
    public async Task MergeNonExistingEventProcessorHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var name = _fixture.Create<EventProcessorName>();
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.MergeEventProcessor(name, TokenStoreIdentifier.Empty));
        Assert.Equal(StatusCode.Cancelled, exception.StatusCode); // REMARK: Why is the status code here cancelled?
    }
    
    [Fact]
    public async Task MoveNonExistingEventProcessorHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var name = _fixture.Create<EventProcessorName>();
        var segmentId = _fixture.Create<SegmentId>();
        var targetClient = _fixture.Create<ClientInstanceId>();
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.MoveEventProcessorSegment(name, TokenStoreIdentifier.Empty, segmentId, targetClient));
        Assert.Equal(StatusCode.NotFound, exception.StatusCode); // REMARK: Why is the status code here cancelled?
    }
    
    [Fact]
    public async Task GetEventProcessorsWhenNoneReturnsExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var actual = await sut.GetEventProcessors().ToListAsync();
        Assert.Empty(actual);
    }
    
    [Fact]
    public async Task GetEventProcessorsByComponentWhenNoneReturnsExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var component = _fixture.Create<ComponentName>();
        var actual = await sut.GetEventProcessorsByComponent(component).ToListAsync();
        Assert.Empty(actual);
    }
    
    // Users

    [Fact(Skip = "Because the axon cluster is reused between invocations, the order tests run in is unpredictable, it's impossible to test this scenario")]
    public async Task GetAllUsersWhenNoneReturnsExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var actual = await sut.GetAllUsers();
        Assert.Empty(actual);
    }
    
    [Fact]
    public async Task CreateUserHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await sut.CreateOrUpdateUser(new CreateOrUpdateUserRequest
        {
            UserName = "user1",
            Password = "p@ssw0rd"
        });
        var actual = await sut.GetAllUsers();
        Assert.Contains(actual, overview => overview.UserName == "user1");
    }
    
    [Fact]
    public async Task UpdateUserHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await sut.CreateOrUpdateUser(new CreateOrUpdateUserRequest
        {
            UserName = "user2",
            Password = "p@ssw0rd1"
        });
        await sut.CreateOrUpdateUser(new CreateOrUpdateUserRequest
        {
            UserName = "user2",
            Password = "p@ssw0rd2"
        });
        var actual = await sut.GetAllUsers();
        Assert.Contains(actual, overview => overview.UserName == "user2");
    }
    
    [Fact]
    public async Task GetAllUsersWhenSomeReturnsExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await sut.CreateOrUpdateUser(new CreateOrUpdateUserRequest
        {
            UserName = "user3",
            Password = "p@ssw0rd"
        });
        await sut.CreateOrUpdateUser(new CreateOrUpdateUserRequest
        {
            UserName = "user4",
            Password = "p@ssw0rd"
        });
        var actual = await sut.GetAllUsers();
        Assert.Contains(actual, overview => overview.UserName == "user3");
        Assert.Contains(actual, overview => overview.UserName == "user4");
    }

    [Fact]
    public async Task DeleteNonExistingUserHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await sut.DeleteUser("non-existing-user");
    }
    
    [Fact]
    public async Task DeleteExistingUserHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await sut.CreateOrUpdateUser(new CreateOrUpdateUserRequest
        {
            UserName = "user5",
            Password = "p@ssw0rd"
        });       
        await sut.DeleteUser("user5");
        var actual = await sut.GetAllUsers();
        Assert.DoesNotContain(actual, overview => overview.UserName == "user5");
    }
    
    // Applications
    
    [Fact]
    public async Task GetAllApplicationsWhenNoneReturnsExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.GetAllApplications());
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task CreateApplicationHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => 
            await sut.CreateOrUpdateApplication(new ApplicationRequest
            {
                ApplicationName = "app1",
                Description = ""
            }));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task GetApplicationHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => 
            await sut.GetApplication("app1"));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task DeleteApplicationHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => 
            await sut.DeleteApplication("app1"));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task RefreshTokenHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => 
            await sut.RefreshToken("app1"));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    // Contexts
    
    [Fact]
    public async Task GetAllContextsWhenNoneReturnsExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.GetAllContexts());
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task CreateContextHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var name = _fixture.Create<Context>().ToString();
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.CreateContext(new CreateContextRequest
        {
            Name = name
        }));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task UpdateContextPropertiesHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var name = _fixture.Create<Context>().ToString();
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.UpdateContextProperties(new UpdateContextPropertiesRequest
        {
            Name = name
        }));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task DeleteContextHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var name = _fixture.Create<Context>().ToString();
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.DeleteContext(new DeleteContextRequest
        {
            Name = name
        }));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task GetContextOverviewHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.GetContextOverview(Context.Default.ToString()));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task GetAllContextsHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.GetAllContexts());
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task SubscribeToContextUpdatesHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var actual = sut.SubscribeToContextUpdates().GetAsyncEnumerator();
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await actual.MoveNextAsync());
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    // Replication groups
    
    [Fact]
    public async Task CreateReplicationGroupHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.CreateReplicationGroup(new CreateReplicationGroupRequest
        {
            Name = "group1"
        }));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task DeleteReplicationGroupHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.DeleteReplicationGroup(new DeleteReplicationGroupRequest
        {
            Name = "group1"
        }));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task GetReplicationGroupHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.GetReplicationGroup("group1"));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task GetAllReplicationGroupsHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.GetAllReplicationGroups());
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task GetAllNodesHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.GetAllNodes());
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task AddNodeToReplicationGroupHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.AddNodeToReplicationGroup(new JoinReplicationGroup
        {
            ReplicationGroupName = "group1",
            NodeName = "node1"
        }));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task RemoveNodeFromReplicationGroupHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.RemoveNodeFromReplicationGroup(new LeaveReplicationGroup
        {
            ReplicationGroupName = "group1",
            NodeName = "node1"
        }));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
}