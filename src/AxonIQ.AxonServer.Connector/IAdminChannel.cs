using Io.Axoniq.Axonserver.Grpc.Admin;

namespace AxonIQ.AxonServer.Connector;

public interface IAdminChannel
{
    // Event Processor management
    IAsyncEnumerable<EventProcessor> GetEventProcessors();
    IAsyncEnumerable<EventProcessor> GetEventProcessorsByComponent(ComponentName component);
    Task<Result> PauseEventProcessorAsync(EventProcessorName name, TokenStoreIdentifier identifier);
    Task<Result> StartEventProcessorAsync(EventProcessorName name, TokenStoreIdentifier identifier);
    Task<Result> SplitEventProcessorAsync(EventProcessorName name, TokenStoreIdentifier identifier);
    Task<Result> MergeEventProcessorAsync(EventProcessorName name, TokenStoreIdentifier identifier);
    Task LoadBalanceEventProcessorAsync(EventProcessorName name, TokenStoreIdentifier identifier, string strategy);
    Task SetAutoLoadBalanceStrategyAsync(EventProcessorName name, TokenStoreIdentifier identifier, string strategy);
    Task<IReadOnlyCollection<LoadBalancingStrategy>> GetBalancingStrategiesAsync();
    Task<Result> MoveEventProcessorSegmentAsync(EventProcessorName name, TokenStoreIdentifier identifier, SegmentId segmentId, ClientInstanceId targetClient);
    
    // User management
    Task CreateOrUpdateUserAsync(CreateOrUpdateUserRequest request);
    Task<IReadOnlyCollection<UserOverview>> GetAllUsersAsync();
    Task DeleteUserAsync(string username);
    
    // Application management
    Task CreateOrUpdateApplicationAsync(ApplicationRequest request);
    Task<ApplicationOverview> GetApplicationAsync(string applicationName);
    Task<IReadOnlyCollection<ApplicationOverview>> GetAllApplicationsAsync();
    Task<Token> RefreshTokenAsync(string applicationName);
    Task DeleteApplicationAsync(string applicationName);
    
    // Context management
    Task CreateContextAsync(CreateContextRequest request);
    Task UpdateContextPropertiesAsync(UpdateContextPropertiesRequest request);
    Task DeleteContextAsync(DeleteContextRequest request);
    Task<ContextOverview> GetContextOverviewAsync(string context);
    Task<IReadOnlyCollection<ContextOverview>> GetAllContextsAsync();
    IAsyncEnumerable<ContextUpdate> SubscribeToContextUpdates();

    // Replication group management
    Task CreateReplicationGroupAsync(CreateReplicationGroupRequest request);
    Task DeleteReplicationGroupAsync(DeleteReplicationGroupRequest request);
    Task<ReplicationGroupOverview> GetReplicationGroupAsync(string replicationGroup);
    Task<IReadOnlyCollection<ReplicationGroupOverview>> GetAllReplicationGroupsAsync();
    Task<IReadOnlyCollection<NodeOverview>> GetAllNodesAsync();
    Task AddNodeToReplicationGroupAsync(JoinReplicationGroup request);
    Task RemoveNodeFromReplicationGroupAsync(LeaveReplicationGroup request);
}