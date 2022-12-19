using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Admin;
using ApplicationId = Io.Axoniq.Axonserver.Grpc.Admin.ApplicationId;

namespace AxonIQ.AxonServer.Connector;

public class AdminChannel : IAdminChannel
{
    public AdminChannel(ClientIdentity clientIdentity, CallInvoker callInvoker)
    {
        if (clientIdentity == null) throw new ArgumentNullException(nameof(clientIdentity));
        if (callInvoker == null) throw new ArgumentNullException(nameof(callInvoker));
        
        ClientIdentity = clientIdentity;
        EventProcessorAdminService = new EventProcessorAdminService.EventProcessorAdminServiceClient(callInvoker);
        ContextAdminService = new ContextAdminService.ContextAdminServiceClient(callInvoker);
        ReplicationGroupAdminService = new ReplicationGroupAdminService.ReplicationGroupAdminServiceClient(callInvoker);
        ApplicationAdminService = new ApplicationAdminService.ApplicationAdminServiceClient(callInvoker);
        UserAdminService = new UserAdminService.UserAdminServiceClient(callInvoker);
    }
    
    public ClientIdentity ClientIdentity { get; }
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

    public async Task<Result> PauseEventProcessor(EventProcessorName name, TokenStoreIdentifier identifier)
    {
        var result = await EventProcessorAdminService.PauseEventProcessorAsync(new EventProcessorIdentifier
        {
            ProcessorName = name.ToString(),
            TokenStoreIdentifier = identifier.ToString()
        }).ConfigureAwait(false);
        return result.Result;
    }

    public async Task<Result> StartEventProcessor(EventProcessorName name, TokenStoreIdentifier identifier)
    {
        var result = await EventProcessorAdminService.StartEventProcessorAsync(new EventProcessorIdentifier
        {
            ProcessorName = name.ToString(),
            TokenStoreIdentifier = identifier.ToString()
        }).ConfigureAwait(false);
        return result.Result;
    }

    public async Task<Result> SplitEventProcessor(EventProcessorName name, TokenStoreIdentifier identifier)
    {
        var result = await EventProcessorAdminService.SplitEventProcessorAsync(new EventProcessorIdentifier
        {
            ProcessorName = name.ToString(),
            TokenStoreIdentifier = identifier.ToString()
        }).ConfigureAwait(false);
        return result.Result;
    }

    public async Task<Result> MergeEventProcessor(EventProcessorName name, TokenStoreIdentifier identifier)
    {
        var result = await EventProcessorAdminService.MergeEventProcessorAsync(new EventProcessorIdentifier
        {
            ProcessorName = name.ToString(),
            TokenStoreIdentifier = identifier.ToString()
        }).ConfigureAwait(false);
        return result.Result;
    }

    public async Task LoadBalanceEventProcessor(EventProcessorName name, TokenStoreIdentifier identifier, string strategy)
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

    public async Task SetAutoLoadBalanceStrategy(EventProcessorName name, TokenStoreIdentifier identifier, string strategy)
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

    public async Task<IReadOnlyCollection<LoadBalancingStrategy>> GetBalancingStrategies()
    {
        using var result = EventProcessorAdminService.GetBalancingStrategies(new Empty());
        return await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task<Result> MoveEventProcessorSegment(EventProcessorName name, TokenStoreIdentifier identifier, SegmentId segmentId,
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

    public async Task CreateOrUpdateUser(CreateOrUpdateUserRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        using var result = UserAdminService.CreateOrUpdateUser(request);
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<UserOverview>> GetAllUsers()
    {
        using var result = UserAdminService.GetUsers(new Empty());
        return await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task DeleteUser(string username)
    {
        using var result = UserAdminService.DeleteUser(new DeleteUserRequest
        {
            UserName = username
        });
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task CreateOrUpdateApplication(ApplicationRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        await ApplicationAdminService.CreateOrUpdateApplicationAsync(request).ConfigureAwait(false);
    }

    public async Task<ApplicationOverview> GetApplication(string applicationName)
    {
        if (applicationName == null) throw new ArgumentNullException(nameof(applicationName));
        return await ApplicationAdminService.GetApplicationAsync(new ApplicationId
        {
            ApplicationName = applicationName
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<ApplicationOverview>> GetAllApplications()
    {
        using var result = ApplicationAdminService.GetApplications(new Empty());
        return await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task<Token> RefreshToken(string applicationName)
    {
        return await ApplicationAdminService.RefreshTokenAsync(new ApplicationId { ApplicationName = applicationName })
            .ConfigureAwait(false);
    }

    public async Task DeleteApplication(string applicationName)
    {
        if (applicationName == null) throw new ArgumentNullException(nameof(applicationName));
        using var result =
            ApplicationAdminService.DeleteApplication(new ApplicationId { ApplicationName = applicationName });
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task CreateContext(CreateContextRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        using var result = ContextAdminService.CreateContext(request);
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task UpdateContextProperties(UpdateContextPropertiesRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        using var result = ContextAdminService.UpdateContextProperties(request);
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task DeleteContext(DeleteContextRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        using var result = ContextAdminService.DeleteContext(request);
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task<ContextOverview> GetContextOverview(string context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        return await ContextAdminService.GetContextAsync(new GetContextRequest
        {
            Name = context
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<ContextOverview>> GetAllContexts()
    {
        using var result = ContextAdminService.GetContexts(new Empty());
        return await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public IAsyncEnumerable<ContextUpdate> SubscribeToContextUpdates()
    {
        var result = ContextAdminService.SubscribeContextUpdates(new Empty());
        return result.ResponseStream.ReadAllAsync();
    }

    public async Task CreateReplicationGroup(CreateReplicationGroupRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        using var result = ReplicationGroupAdminService.CreateReplicationGroup(request);
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task DeleteReplicationGroup(DeleteReplicationGroupRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        using var result = ReplicationGroupAdminService.DeleteReplicationGroup(request);
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task<ReplicationGroupOverview> GetReplicationGroup(string replicationGroup)
    {
        if (replicationGroup == null) throw new ArgumentNullException(nameof(replicationGroup));
        return await ReplicationGroupAdminService.GetReplicationGroupAsync(new GetReplicationGroupRequest
        {
            Name = replicationGroup
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<ReplicationGroupOverview>> GetAllReplicationGroups()
    {
        var result = ReplicationGroupAdminService.GetReplicationGroups(new Empty());
        return await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<NodeOverview>> GetAllNodes()
    {
        var result = ReplicationGroupAdminService.GetNodes(new Empty());
        return await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task AddNodeToReplicationGroup(JoinReplicationGroup request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        using var result = ReplicationGroupAdminService.AddNodeToReplicationGroup(request);
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task RemoveNodeFromReplicationGroup(LeaveReplicationGroup request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        using var result = ReplicationGroupAdminService.RemoveNodeFromReplicationGroup(request);
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }
}