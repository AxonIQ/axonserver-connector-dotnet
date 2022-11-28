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

    public Task LoadBalanceEventProcessor(EventProcessorName name, TokenStoreIdentifier identifier, string strategy)
    {
        //TODO: Why was this designed this way and are we supposed to read any results or somehow wait for it to complete?
        using var result = EventProcessorAdminService.LoadBalanceProcessor(new LoadBalanceRequest
        {
            Processor = new EventProcessorIdentifier
            {
                ProcessorName = name.ToString(),
                TokenStoreIdentifier = identifier.ToString()
            },
            Strategy = strategy
        });
        return Task.CompletedTask;
    }

    public async Task SetAutoLoadBalanceStrategy(EventProcessorName name, TokenStoreIdentifier identifier, string strategy)
    {
        //TODO: Why was this designed this way and are we supposed to read any results or somehow wait for it to complete?
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
        await ApplicationAdminService.CreateOrUpdateApplicationAsync(request).ConfigureAwait(false);
    }

    public async Task<ApplicationOverview> GetApplication(string applicationName)
    {
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
        using var result =
            ApplicationAdminService.DeleteApplication(new ApplicationId { ApplicationName = applicationName });
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public async Task CreateContext(CreateContextRequest request)
    {
        using var result = ContextAdminService.CreateContext(request);
        await result.ResponseStream.ReadAllAsync().ToListAsync().ConfigureAwait(false);
    }

    public Task UpdateContextProperties(UpdateContextPropertiesRequest request)
    {
        throw new NotImplementedException();
    }

    public Task DeleteContext(DeleteContextRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<ContextOverview> GetContextOverview(string context)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyCollection<ContextOverview>> GetAllContexts()
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<ContextUpdate> SubscribeToContextUpdates()
    {
        throw new NotImplementedException();
    }

    public Task CreateReplicationGroup(CreateReplicationGroupRequest request)
    {
        throw new NotImplementedException();
    }

    public Task DeleteReplicationGroup(DeleteReplicationGroupRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<ReplicationGroupOverview> GetReplicationGroup(string replicationGroup)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyCollection<ReplicationGroupOverview>> GetAllReplicationGroups()
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyCollection<NodeOverview>> GetAllNodes()
    {
        throw new NotImplementedException();
    }

    public Task AddNodeToReplicationGroup(JoinReplicationGroup request)
    {
        throw new NotImplementedException();
    }

    public Task RemoveNodeFromReplicationGroup(LeaveReplicationGroup request)
    {
        throw new NotImplementedException();
    }
}