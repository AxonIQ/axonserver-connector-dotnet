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
using AsyncEnumerable = System.Linq.AsyncEnumerable;

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
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.StartEventProcessorAsync(name, TokenStoreIdentifier.Empty));
        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }
    
    [Fact]
    public async Task PauseNonExistingEventProcessorHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var name = _fixture.Create<EventProcessorName>();
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.PauseEventProcessorAsync(name, TokenStoreIdentifier.Empty));
        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }
    
    [Fact]
    public async Task SplitNonExistingEventProcessorHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var name = _fixture.Create<EventProcessorName>();
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.SplitEventProcessorAsync(name, TokenStoreIdentifier.Empty));
        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
    }
    
    [Fact]
    public async Task MergeNonExistingEventProcessorHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var name = _fixture.Create<EventProcessorName>();
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.MergeEventProcessorAsync(name, TokenStoreIdentifier.Empty));
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
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.MoveEventProcessorSegmentAsync(name, TokenStoreIdentifier.Empty, segmentId, targetClient));
        Assert.Equal(StatusCode.NotFound, exception.StatusCode); // REMARK: Why is the status code here cancelled?
    }
    
    [Fact]
    public async Task GetEventProcessorsWhenNoneReturnsExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var actual = await AsyncEnumerable.ToListAsync(sut.GetEventProcessors());
        Assert.Empty(actual);
    }
    
    [Fact]
    public async Task GetEventProcessorsByComponentWhenNoneReturnsExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var component = _fixture.Create<ComponentName>();
        var actual = await AsyncEnumerable.ToListAsync(sut.GetEventProcessorsByComponent(component));
        Assert.Empty(actual);
    }
    
    // Users

    [Fact(Skip = "Because the axon server is reused between invocations, the order tests run in is unpredictable, it's impossible to test this scenario")]
    public async Task GetAllUsersWhenNoneReturnsExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var actual = await sut.GetAllUsersAsync();
        Assert.Empty(actual);
    }
    
    [Fact]
    public async Task CreateUserHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await sut.CreateOrUpdateUserAsync(new CreateOrUpdateUserRequest
        {
            UserName = "user1",
            Password = "p@ssw0rd"
        });
        var actual = await sut.GetAllUsersAsync();
        Assert.Contains(actual, overview => overview.UserName == "user1");
    }
    
    [Fact]
    public async Task UpdateUserHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await sut.CreateOrUpdateUserAsync(new CreateOrUpdateUserRequest
        {
            UserName = "user2",
            Password = "p@ssw0rd1"
        });
        await sut.CreateOrUpdateUserAsync(new CreateOrUpdateUserRequest
        {
            UserName = "user2",
            Password = "p@ssw0rd2"
        });
        var actual = await sut.GetAllUsersAsync();
        Assert.Contains(actual, overview => overview.UserName == "user2");
    }
    
    [Fact]
    public async Task GetAllUsersWhenSomeReturnsExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await sut.CreateOrUpdateUserAsync(new CreateOrUpdateUserRequest
        {
            UserName = "user3",
            Password = "p@ssw0rd"
        });
        await sut.CreateOrUpdateUserAsync(new CreateOrUpdateUserRequest
        {
            UserName = "user4",
            Password = "p@ssw0rd"
        });
        var actual = await sut.GetAllUsersAsync();
        Assert.Contains(actual, overview => overview.UserName == "user3");
        Assert.Contains(actual, overview => overview.UserName == "user4");
    }

    [Fact]
    public async Task DeleteNonExistingUserHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await sut.DeleteUserAsync("non-existing-user");
    }
    
    [Fact]
    public async Task DeleteExistingUserHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await sut.CreateOrUpdateUserAsync(new CreateOrUpdateUserRequest
        {
            UserName = "user5",
            Password = "p@ssw0rd"
        });       
        await sut.DeleteUserAsync("user5");
        var actual = await sut.GetAllUsersAsync();
        Assert.DoesNotContain(actual, overview => overview.UserName == "user5");
    }
    
    // Applications
    
    [Fact]
    public async Task GetAllApplicationsWhenNoneReturnsExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.GetAllApplicationsAsync());
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task CreateApplicationHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => 
            await sut.CreateOrUpdateApplicationAsync(new ApplicationRequest
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
            await sut.GetApplicationAsync("app1"));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task DeleteApplicationHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => 
            await sut.DeleteApplicationAsync("app1"));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task RefreshTokenHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => 
            await sut.RefreshTokenAsync("app1"));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    // Contexts
    
    [Fact]
    public async Task GetAllContextsWhenNoneReturnsExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.GetAllContextsAsync());
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task CreateContextHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var name = _fixture.Create<Context>().ToString();
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.CreateContextAsync(new CreateContextRequest
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
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.UpdateContextPropertiesAsync(new UpdateContextPropertiesRequest
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
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.DeleteContextAsync(new DeleteContextRequest
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
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.GetContextOverviewAsync(Context.Default.ToString()));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task GetAllContextsHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.GetAllContextsAsync());
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
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.CreateReplicationGroupAsync(new CreateReplicationGroupRequest
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
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.DeleteReplicationGroupAsync(new DeleteReplicationGroupRequest
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
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.GetReplicationGroupAsync("group1"));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task GetAllReplicationGroupsHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.GetAllReplicationGroupsAsync());
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task GetAllNodesHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.GetAllNodesAsync());
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
    
    [Fact]
    public async Task AddNodeToReplicationGroupHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.AddNodeToReplicationGroupAsync(new JoinReplicationGroup
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
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.RemoveNodeFromReplicationGroupAsync(new LeaveReplicationGroup
        {
            ReplicationGroupName = "group1",
            NodeName = "node1"
        }));
        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }
}