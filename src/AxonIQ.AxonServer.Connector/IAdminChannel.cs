using Io.Axoniq.Axonserver.Grpc.Admin;

namespace AxonIQ.AxonServer.Connector;

public interface IAdminChannel
{
    // Event Processor management
    IAsyncEnumerable<EventProcessor> GetEventProcessors();
    IAsyncEnumerable<EventProcessor> GetEventProcessorsByComponent(ComponentName component);
    Task<Result> PauseEventProcessor(EventProcessorName name, TokenStoreIdentifier identifier);
    Task<Result> StartEventProcessor(EventProcessorName name, TokenStoreIdentifier identifier);
    Task<Result> SplitEventProcessor(EventProcessorName name, TokenStoreIdentifier identifier);
    Task<Result> MergeEventProcessor(EventProcessorName name, TokenStoreIdentifier identifier);
    Task LoadBalanceEventProcessor(EventProcessorName name, TokenStoreIdentifier identifier, string strategy);
    Task SetAutoLoadBalanceStrategy(EventProcessorName name, TokenStoreIdentifier identifier, string strategy);
    Task<IReadOnlyCollection<LoadBalancingStrategy>> GetBalancingStrategies();
    Task<Result> MoveEventProcessorSegment(EventProcessorName name, TokenStoreIdentifier identifier, SegmentId segmentId, ClientInstanceId targetClient);
    
    // User management
    Task CreateOrUpdateUser(CreateOrUpdateUserRequest request);
    Task<IReadOnlyCollection<UserOverview>> GetAllUsers();
    Task DeleteUser(string username);
    
    // Application management
    Task CreateOrUpdateApplication(ApplicationRequest request);
    Task<ApplicationOverview> GetApplication(string applicationName);
    Task<IReadOnlyCollection<ApplicationOverview>> GetAllApplications();
    Task<Token> RefreshToken(string applicationName);
    Task DeleteApplication(string applicationName);
    
    // Context management
    Task CreateContext(CreateContextRequest request);
    Task UpdateContextProperties(UpdateContextPropertiesRequest request);
    Task DeleteContext(DeleteContextRequest request);
    Task<ContextOverview> GetContextOverview(string context);
    Task<IReadOnlyCollection<ContextOverview>> GetAllContexts();
    IAsyncEnumerable<ContextUpdate> SubscribeToContextUpdates();

    // Replication group management
    Task CreateReplicationGroup(CreateReplicationGroupRequest request);
    Task DeleteReplicationGroup(DeleteReplicationGroupRequest request);
    Task<ReplicationGroupOverview> GetReplicationGroup(string replicationGroup);
    Task<IReadOnlyCollection<ReplicationGroupOverview>> GetAllReplicationGroups();
    Task<IReadOnlyCollection<NodeOverview>> GetAllNodes();
    Task AddNodeToReplicationGroup(JoinReplicationGroup request);
    Task RemoveNodeFromReplicationGroup(LeaveReplicationGroup request);
}