using AutoFixture;
using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Connector.Tests;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using AxonIQ.AxonServerIntegrationTests.Containerization;
using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc.Admin;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace AxonIQ.AxonServerIntegrationTests;

[Trait("Surface", "AdminChannel")]
public class AxonServerAdminChannelIntegrationTests : IAsyncLifetime
{
    private readonly IAxonServer _container;
    private readonly Fixture _fixture;
    private readonly ILoggerFactory _loggerFactory;

    public AxonServerAdminChannelIntegrationTests(ITestOutputHelper output)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        _container = EmbeddedAxonServer.WithAccessControlDisabled(new TestOutputHelperLogger<EmbeddedAxonServer>(output));
        _fixture = new Fixture();
        _fixture.CustomizeClientInstanceId();
        _fixture.CustomizeComponentName();
        _fixture.CustomizeEventProcessorName();
        _fixture.CustomizeSegmentId();
        _loggerFactory = new TestOutputHelperLoggerFactory(output);
    }

    private async Task<IAxonServerConnection> CreateSystemUnderTest(
        Action<IAxonServerConnectorOptionsBuilder>? configure = default)
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientInstanceId>();

        var builder = AxonServerConnectorOptions.For(component, clientInstance)
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
        await Task.Delay(500);
        var actual = await sut.GetAllUsersAsync();
        Assert.DoesNotContain(actual, overview => overview.UserName == "user5");
    }
    
    // Applications
    
    [Fact]
    public async Task GetAllApplicationsReturnsExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var actual = await sut.GetAllApplicationsAsync();
        Assert.Empty(actual);
    }
    
    [Fact]
    public async Task CreateApplicationHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await sut.CreateOrUpdateApplicationAsync(new ApplicationRequest
        {
            ApplicationName = "app1",
            Description = ""
        });
        await Task.Delay(500);
        var actual = await sut.GetAllApplicationsAsync();
        Assert.Contains(actual, overview => overview.ApplicationName == "app1");
    }
    
    [Fact]
    public async Task DeleteApplicationHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await sut.CreateOrUpdateApplicationAsync(new ApplicationRequest
        {
            ApplicationName = "app2",
            Description = ""
        });
        await sut.DeleteApplicationAsync("app2");
        await Task.Delay(500);
        var actual = await sut.GetAllApplicationsAsync();
        Assert.DoesNotContain(actual, overview => overview.ApplicationName == "app2");
    }
    
    // Contexts
    
    [Fact]
    public async Task GetAllContextsReturnsExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var actual = await sut.GetAllContextsAsync();
        Assert.NotEmpty(actual);
    }
    
    [Fact]
    public async Task CreateContextHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var name = _fixture.Create<Context>().ToString();
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.CreateContextAsync(new CreateContextRequest
        {
            Name = name,
            ReplicationGroupName = Context.Default.ToString()
        }));
        Assert.Equal(StatusCode.PermissionDenied, exception.StatusCode);
        Assert.Equal("[AXONIQ-1700] Maximum number of contexts reached", exception.Status.Detail);
    }

    [Fact]
    public async Task UpdateContextPropertiesHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await Task.Delay(500);
        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await sut.UpdateContextPropertiesAsync(new UpdateContextPropertiesRequest
            {
                Name = Context.Default.ToString(),
                MetaData = { { "Key", "Value" } }
            }));
        Assert.Equal(StatusCode.PermissionDenied, exception.StatusCode);
        Assert.Equal("[AXONIQ-1700] Updating a context is not supported in this edition", exception.Status.Detail);
    }
    
    [Fact(Skip = "Executing this test in conjunction with other tests causes the shared server instance to no longer have a default context.")]
    public async Task DeleteContextHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await sut.DeleteContextAsync(new DeleteContextRequest { Name = Context.Default.ToString() });
        await Task.Delay(500);
        var actual = await sut.GetAllContextsAsync();
        Assert.DoesNotContain(actual, overview => overview.Name == Context.Default.ToString());
    }
    
    [Fact]
    public async Task GetContextOverviewHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var actual = await sut.GetContextOverviewAsync(Context.Default.ToString());
        Assert.Equal(Context.Default.ToString(), actual.Name);
    }
    
    [Fact]
    public async Task GetAllContextsHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var actual = await sut.GetAllContextsAsync();
        Assert.Contains(actual, overview => overview.Name == Context.Default.ToString());
        Assert.Contains(actual, overview => overview.Name == Context.Admin.ToString());
    }
    
    
    [Fact]
    public async Task SubscribeToContextUpdatesHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await sut.DeleteContextAsync(new DeleteContextRequest
        {
            Name = Context.Default.ToString()
        });
        await Task.Delay(1000);
        var name = _fixture.Create<Context>().ToString();
        var updates = new List<ContextUpdate>();
        var subscriber = Task.Run(async () =>
        {
            var endOfEnumeration = false;
            await using var enumerator = sut.SubscribeToContextUpdates().GetAsyncEnumerator();
            while (!endOfEnumeration && await enumerator.MoveNextAsync())
            {
                updates.Add(enumerator.Current);
                if (enumerator.Current.Context == name && enumerator.Current.Type == ContextUpdateType.Created)
                {
                    endOfEnumeration = true;
                }
            }
        });
        await sut.CreateContextAsync(new CreateContextRequest
        {
            Name = name,
            ReplicationGroupName = Context.Default.ToString()
        });
        await subscriber;
        Assert.Contains(updates, update => update.Context == name && update.Type == ContextUpdateType.Created);
    }
    
    // Replication groups
    
    [Fact]
    public async Task CreateReplicationGroupHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.CreateReplicationGroupAsync(new CreateReplicationGroupRequest
        {
            Name = "group1",
            Members = { new ReplicationGroupMember
            {
                NodeName = _container.Properties.NodeSetup.Name,
                Host = _container.Properties.NodeSetup.Hostname,
                Port = _container.Properties.NodeSetup.Port ?? 8124
            } }
        }));
        Assert.Equal(StatusCode.PermissionDenied, exception.StatusCode);
        Assert.Equal("[AXONIQ-1700] Maximum number of replication groups reached", exception.Status.Detail);
    }
    
    
    [Fact]
    public async Task DeleteReplicationGroupHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await sut.DeleteReplicationGroupAsync(new DeleteReplicationGroupRequest
        {
            Name = Context.Default.ToString()
        });
        await Task.Delay(500);
        var actual = await sut.GetAllReplicationGroupsAsync();
        Assert.DoesNotContain(actual, overview => overview.Name == Context.Default.ToString());
    }
    
    [Fact]
    public async Task GetReplicationGroupHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        await connection.WaitUntilReadyAsync();
        var sut = connection.AdminChannel;
        var actual = await sut.GetReplicationGroupAsync(Context.Default.ToString());
        Assert.NotNull(actual);
    }
    
    [Fact]
    public async Task GetAllReplicationGroupsHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        await connection.WaitUntilReadyAsync();
        var sut = connection.AdminChannel;
        var actual = await sut.GetAllReplicationGroupsAsync();
        Assert.Contains(actual, overview => overview.Name == Context.Default.ToString());
        Assert.Contains(actual, overview => overview.Name == Context.Admin.ToString());
    }
    
    [Fact]
    public async Task GetAllNodesHasExpectedResult()
    {
        var names = new []{_container.Properties.NodeSetup.Name};
        var connection = await CreateSystemUnderTest();
        await connection.WaitUntilReadyAsync();
        var sut = connection.AdminChannel;
        var actual = await sut.GetAllNodesAsync();
        foreach (var name in names)
        {
            Assert.Contains(actual, overview => overview.NodeName == name);    
        }
    }
    
    // [Fact]
    // public async Task AddNodeToReplicationGroupHasExpectedResult()
    // {
    //     var connection = await CreateSystemUnderTest();
    //     await connection.WaitUntilReady();
    //     var sut = connection.AdminChannel;
    //     var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.AddNodeToReplicationGroup(new JoinReplicationGroup
    //     {
    //         ReplicationGroupName = "group1",
    //         NodeName = "node1"
    //     }));
    //     Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    // }
    //
    // [Fact]
    // public async Task RemoveNodeFromReplicationGroupHasExpectedResult()
    // {
    //     var connection = await CreateSystemUnderTest();
    //     await connection.WaitUntilReady();
    //     var sut = connection.AdminChannel;
    //     var exception = await Assert.ThrowsAsync<RpcException>(async () => await sut.RemoveNodeFromReplicationGroup(new LeaveReplicationGroup
    //     {
    //         ReplicationGroupName = "group1",
    //         NodeName = "node1"
    //     }));
    //     Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    // }

    public async Task InitializeAsync()
    {
        await _container.InitializeAsync();
        await _container.WaitUntilAvailableAsync();
    }
    
    public Task DisposeAsync() => _container.DisposeAsync();
}