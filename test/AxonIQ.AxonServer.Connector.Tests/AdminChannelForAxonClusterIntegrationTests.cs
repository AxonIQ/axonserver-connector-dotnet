using AutoFixture;
using AxonIQ.AxonServer.Connector.Tests.Containerization;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc.Admin;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests;

[Collection(nameof(AxonClusterWithAccessControlDisabledCollection))]
public class AdminChannelForAxonClusterIntegrationTests
{
    private readonly IAxonCluster _cluster;
    private readonly Fixture _fixture;
    private readonly ILoggerFactory _loggerFactory;

    public AdminChannelForAxonClusterIntegrationTests(AxonClusterWithAccessControlDisabled cluster, ITestOutputHelper output)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _fixture = new Fixture();
        _fixture.CustomizeClientInstanceId();
        _fixture.CustomizeComponentName();
        _fixture.CustomizeEventProcessorName();
        _fixture.CustomizeSegmentId();
        _fixture.CustomizeContext();
        _loggerFactory = new TestOutputHelperLoggerFactory(output);
    }

    private async Task<IAxonServerConnection> CreateSystemUnderTest(
        Action<IAxonServerConnectionFactoryOptionsBuilder>? configure = default)
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientInstanceId>();

        var builder = AxonServerConnectionFactoryOptions.For(component, clientInstance)
            .WithRoutingServers(_cluster.GetRandomGrpcEndpoint())
            .WithLoggerFactory(_loggerFactory);
        configure?.Invoke(builder);
        var options = builder.Build();
        var factory = new AxonServerConnectionFactory(options);
        var connection = await factory.Connect(Context.Default);
        await connection.WaitUntilReady();
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

    [Fact]
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
        await Task.Delay(500);
        var actual = await sut.GetAllUsers();
        Assert.DoesNotContain(actual, overview => overview.UserName == "user5");
    }
    
    // Applications
    
    [Fact]
    public async Task GetAllApplicationsReturnsExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var actual = await sut.GetAllApplications();
        Assert.NotEmpty(actual);
    }
    
    [Fact]
    public async Task CreateApplicationHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await sut.CreateOrUpdateApplication(new ApplicationRequest
        {
            ApplicationName = "app1",
            Description = ""
        });
        await Task.Delay(500);
        var actual = await sut.GetAllApplications();
        Assert.Contains(actual, overview => overview.ApplicationName == "app1");
    }
    
    [Fact]
    public async Task DeleteApplicationHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await sut.CreateOrUpdateApplication(new ApplicationRequest
        {
            ApplicationName = "app2",
            Description = ""
        });
        await sut.DeleteApplication("app2");
        await Task.Delay(500);
        var actual = await sut.GetAllApplications();
        Assert.DoesNotContain(actual, overview => overview.ApplicationName == "app2");
    }
    
    // Contexts
    
    [Fact]
    public async Task GetAllContextsReturnsExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var actual = await sut.GetAllContexts();
        Assert.NotEmpty(actual);
    }
    
    [Fact]
    public async Task CreateContextHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var name = _fixture.Create<Context>().ToString();
        await sut.CreateContext(new CreateContextRequest
        {
            Name = name,
            ReplicationGroupName = Context.Default.ToString()
        });
        await Task.Delay(500);
        var actual = await sut.GetAllContexts();
        Assert.Contains(actual, overview => overview.Name == name);
    }
    
    [Fact]
    public async Task UpdateContextPropertiesHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var name = _fixture.Create<Context>().ToString();
        await sut.CreateContext(new CreateContextRequest
        {
            Name = name,
            ReplicationGroupName = Context.Default.ToString()
        });
        await Task.Delay(500);
        await sut.UpdateContextProperties(new UpdateContextPropertiesRequest
        {
            Name = name,
            MetaData = { { "Key", "Value" } }
        });
        await Task.Delay(500);
        var actual = await sut.GetContextOverview(name);
        Assert.Contains(actual.MetaData, pair => pair is { Key: "Key", Value: "Value" });
    }
    
    [Fact]
    public async Task DeleteContextHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var name = _fixture.Create<Context>().ToString();
        await sut.CreateContext(new CreateContextRequest
        {
            Name = name,
            ReplicationGroupName = Context.Default.ToString()
        });
        await Task.Delay(500);
        await sut.DeleteContext(new DeleteContextRequest { Name = name });
        await Task.Delay(500);
        var actual = await sut.GetAllContexts();
        Assert.DoesNotContain(actual, overview => overview.Name == name);
    }
    
    [Fact]
    public async Task GetContextOverviewHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var actual = await sut.GetContextOverview(Context.Default.ToString());
        Assert.Equal(Context.Default.ToString(), actual.Name);
    }
    
    [Fact]
    public async Task GetAllContextsHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        var actual = await sut.GetAllContexts();
        Assert.Contains(actual, overview => overview.Name == Context.Default.ToString());
        Assert.Contains(actual, overview => overview.Name == Context.Admin.ToString());
    }
    
    
    [Fact]
    public async Task SubscribeToContextUpdatesHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
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
        await sut.CreateContext(new CreateContextRequest
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
        await sut.CreateReplicationGroup(new CreateReplicationGroupRequest
        {
            Name = "group1",
            Members = { new ReplicationGroupMember
            {
                NodeName = _cluster.Nodes[0].Properties.NodeSetup.Name,
                Host = _cluster.Nodes[0].Properties.NodeSetup.Hostname,
                Port = _cluster.Nodes[0].Properties.NodeSetup.Port ?? 8124
            } }
        });
        await Task.Delay(500);
        var actual = await sut.GetAllReplicationGroups();
        Assert.Contains(actual, overview => overview.Name == "group1");
    }
    
    
    [Fact]
    public async Task DeleteReplicationGroupHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.AdminChannel;
        await sut.CreateReplicationGroup(new CreateReplicationGroupRequest
        {
            Name = "group2",
            Members = { new ReplicationGroupMember
            {
                NodeName = _cluster.Nodes[0].Properties.NodeSetup.Name,
                Host = _cluster.Nodes[0].Properties.NodeSetup.Hostname,
                Port = _cluster.Nodes[0].Properties.NodeSetup.Port ?? 8124
            } }
        });
        await Task.Delay(500);
        await sut.DeleteReplicationGroup(new DeleteReplicationGroupRequest
        {
            Name = "group2"
        });
        await Task.Delay(500);
        var actual = await sut.GetAllReplicationGroups();
        Assert.DoesNotContain(actual, overview => overview.Name == "group2");
    }
    
    [Fact]
    public async Task GetReplicationGroupHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        await connection.WaitUntilReady();
        var sut = connection.AdminChannel;
        await sut.CreateReplicationGroup(new CreateReplicationGroupRequest
        {
            Name = "group3",
            Members = { new ReplicationGroupMember
            {
                NodeName = _cluster.Nodes[0].Properties.NodeSetup.Name,
                Host = _cluster.Nodes[0].Properties.NodeSetup.Hostname,
                Port = _cluster.Nodes[0].Properties.NodeSetup.Port ?? 8124
            } }
        });
        await Task.Delay(500);
        var actual = await sut.GetReplicationGroup("group3");
        Assert.NotNull(actual);
    }
    
    [Fact]
    public async Task GetAllReplicationGroupsHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        await connection.WaitUntilReady();
        var sut = connection.AdminChannel;
        var actual = await sut.GetAllReplicationGroups();
        Assert.Contains(actual, overview => overview.Name == Context.Default.ToString());
        Assert.Contains(actual, overview => overview.Name == Context.Admin.ToString());
    }
    
    [Fact]
    public async Task GetAllNodesHasExpectedResult()
    {
        var names = _cluster.Nodes.Select(node => node.Properties.NodeSetup.Name).ToArray();
        var connection = await CreateSystemUnderTest();
        await connection.WaitUntilReady();
        var sut = connection.AdminChannel;
        var actual = await sut.GetAllNodes();
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
}