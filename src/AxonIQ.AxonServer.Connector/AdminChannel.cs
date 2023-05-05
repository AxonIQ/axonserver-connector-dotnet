using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Admin;
using ApplicationId = Io.Axoniq.Axonserver.Grpc.Admin.ApplicationId;

namespace AxonIQ.AxonServer.Connector;

internal class AdminChannel : IAdminChannel
{
    public AdminChannel(AxonServerConnection connection)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));

        EventProcessorAdminService = new EventProcessorAdminService.EventProcessorAdminServiceClient(connection.CallInvoker);
        ContextAdminService = new ContextAdminService.ContextAdminServiceClient(connection.CallInvoker);
        ReplicationGroupAdminService = new ReplicationGroupAdminService.ReplicationGroupAdminServiceClient(connection.CallInvoker);
        ApplicationAdminService = new ApplicationAdminService.ApplicationAdminServiceClient(connection.CallInvoker);
        UserAdminService = new UserAdminService.UserAdminServiceClient(connection.CallInvoker);
    }
    
    public EventProcessorAdminService.EventProcessorAdminServiceClient EventProcessorAdminService { get; }
    public ContextAdminService.ContextAdminServiceClient ContextAdminService { get; }
    public ReplicationGroupAdminService.ReplicationGroupAdminServiceClient ReplicationGroupAdminService { get; }
    public ApplicationAdminService.ApplicationAdminServiceClient ApplicationAdminService { get; }
    public UserAdminService.UserAdminServiceClient UserAdminService { get; }
    
    public IAsyncEnumerable<EventProcessor> GetEventProcessors()
    {
        var result = EventProcessorAdminService.GetAllEventProcessors(new Empty());
        return result.ResponseStream.ReadAllAsync();
    }

    public IAsyncEnumerable<EventProcessor> GetEventProcessorsByComponent(ComponentName component)
    {
        var result = EventProcessorAdminService.GetEventProcessorsByComponent(new Component
        {
            Component_ = component.ToString()
        });
        return result.ResponseStream.ReadAllAsync();
    }

    public async Task<Result> PauseEventProcessorAsync(EventProcessorName name, TokenStoreIdentifier identifier)
    {
        var result = await EventProcessorAdminService.PauseEventProcessorAsync(new EventProcessorIdentifier
        {
            ProcessorName = name.ToString(),
            TokenStoreIdentifier = identifier.ToString()
        }).ConfigureAwait(false);
        return result.Result;
    }

    public async Task<Result> StartEventProcessorAsync(EventProcessorName name, TokenStoreIdentifier identifier)
    {
        var result = await EventProcessorAdminService.StartEventProcessorAsync(new EventProcessorIdentifier
        {
            ProcessorName = name.ToString(),
            TokenStoreIdentifier = identifier.ToString()
        }).ConfigureAwait(false);
        return result.Result;
    }

    public async Task<Result> SplitEventProcessorAsync(EventProcessorName name, TokenStoreIdentifier identifier)
    {
        var result = await EventProcessorAdminService.SplitEventProcessorAsync(new EventProcessorIdentifier
        {
            ProcessorName = name.ToString(),
            TokenStoreIdentifier = identifier.ToString()
        }).ConfigureAwait(false);
        return result.Result;
    }

    public async Task<Result> MergeEventProcessorAsync(EventProcessorName name, TokenStoreIdentifier identifier)
    {
        var result = await EventProcessorAdminService.MergeEventProcessorAsync(new EventProcessorIdentifier
        {
            ProcessorName = name.ToString(),
            TokenStoreIdentifier = identifier.ToString()
        }).ConfigureAwait(false);
        return result.Result;
    }

    public async Task LoadBalanceEventProcessorAsync(EventProcessorName name, TokenStoreIdentifier identifier, string strategy)
    {
        using var result = EventProcessorAdminService.LoadBalanceProcessor(new LoadBalanceRequest
        {
            Processor = new EventProcessorIdentifier
            {
                ProcessorName = name.ToString(),
                TokenStoreIdentifier = identifier.ToString()
            },
            Strategy = strategy
        });
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task SetAutoLoadBalanceStrategyAsync(EventProcessorName name, TokenStoreIdentifier identifier, string strategy)
    {
        using var result = EventProcessorAdminService.SetAutoLoadBalanceStrategy(new LoadBalanceRequest
        {
            Processor = new EventProcessorIdentifier
            {
                ProcessorName = name.ToString(),
                TokenStoreIdentifier = identifier.ToString()
            },
            Strategy = strategy
        });
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<LoadBalancingStrategy>> GetBalancingStrategiesAsync()
    {
        using var result = EventProcessorAdminService.GetBalancingStrategies(new Empty());
        return await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task<Result> MoveEventProcessorSegmentAsync(EventProcessorName name, TokenStoreIdentifier identifier, SegmentId segmentId,
        ClientInstanceId targetClient)
    {
        var result = await EventProcessorAdminService.MoveEventProcessorSegmentAsync(new MoveSegment
        {
            EventProcessor = new EventProcessorIdentifier
            {
                ProcessorName = name.ToString(),
                TokenStoreIdentifier = identifier.ToString()
            },
            Segment = segmentId.ToInt32(),
            TargetClientId = targetClient.ToString()
        }).ConfigureAwait(false);
        return result.Result;
    }

    public async Task CreateOrUpdateUserAsync(CreateOrUpdateUserRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        using var result = UserAdminService.CreateOrUpdateUser(request);
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<UserOverview>> GetAllUsersAsync()
    {
        using var result = UserAdminService.GetUsers(new Empty());
        return await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task DeleteUserAsync(string username)
    {
        using var result = UserAdminService.DeleteUser(new DeleteUserRequest
        {
            UserName = username
        });
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task CreateOrUpdateApplicationAsync(ApplicationRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        await ApplicationAdminService.CreateOrUpdateApplicationAsync(request).ConfigureAwait(false);
    }

    public async Task<ApplicationOverview> GetApplicationAsync(string applicationName)
    {
        if (applicationName == null) throw new ArgumentNullException(nameof(applicationName));
        return await ApplicationAdminService.GetApplicationAsync(new ApplicationId
        {
            ApplicationName = applicationName
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<ApplicationOverview>> GetAllApplicationsAsync()
    {
        using var result = ApplicationAdminService.GetApplications(new Empty());
        return await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task<Token> RefreshTokenAsync(string applicationName)
    {
        return await ApplicationAdminService.RefreshTokenAsync(new ApplicationId { ApplicationName = applicationName })
            .ConfigureAwait(false);
    }

    public async Task DeleteApplicationAsync(string applicationName)
    {
        if (applicationName == null) throw new ArgumentNullException(nameof(applicationName));
        using var result =
            ApplicationAdminService.DeleteApplication(new ApplicationId { ApplicationName = applicationName });
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task CreateContextAsync(CreateContextRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        using var result = ContextAdminService.CreateContext(request);
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task UpdateContextPropertiesAsync(UpdateContextPropertiesRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        using var result = ContextAdminService.UpdateContextProperties(request);
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task DeleteContextAsync(DeleteContextRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        using var result = ContextAdminService.DeleteContext(request);
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task<ContextOverview> GetContextOverviewAsync(string context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        return await ContextAdminService.GetContextAsync(new GetContextRequest
        {
            Name = context
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<ContextOverview>> GetAllContextsAsync()
    {
        using var result = ContextAdminService.GetContexts(new Empty());
        return await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public IAsyncEnumerable<ContextUpdate> SubscribeToContextUpdates()
    {
        var result = ContextAdminService.SubscribeContextUpdates(new Empty());
        return result.ResponseStream.ReadAllAsync();
    }

    public async Task CreateReplicationGroupAsync(CreateReplicationGroupRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        using var result = ReplicationGroupAdminService.CreateReplicationGroup(request);
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task DeleteReplicationGroupAsync(DeleteReplicationGroupRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        using var result = ReplicationGroupAdminService.DeleteReplicationGroup(request);
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task<ReplicationGroupOverview> GetReplicationGroupAsync(string replicationGroup)
    {
        if (replicationGroup == null) throw new ArgumentNullException(nameof(replicationGroup));
        return await ReplicationGroupAdminService.GetReplicationGroupAsync(new GetReplicationGroupRequest
        {
            Name = replicationGroup
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<ReplicationGroupOverview>> GetAllReplicationGroupsAsync()
    {
        var result = ReplicationGroupAdminService.GetReplicationGroups(new Empty());
        return await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<NodeOverview>> GetAllNodesAsync()
    {
        var result = ReplicationGroupAdminService.GetNodes(new Empty());
        return await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task AddNodeToReplicationGroupAsync(JoinReplicationGroup request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        using var result = ReplicationGroupAdminService.AddNodeToReplicationGroup(request);
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task RemoveNodeFromReplicationGroupAsync(LeaveReplicationGroup request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        using var result = ReplicationGroupAdminService.RemoveNodeFromReplicationGroup(request);
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }
}