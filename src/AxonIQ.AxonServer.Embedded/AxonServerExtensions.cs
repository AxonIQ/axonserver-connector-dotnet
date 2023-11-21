using AxonIQ.AxonServer.Connector;
using Io.Axoniq.Axonserver.Grpc.Admin;

namespace AxonIQ.AxonServer.Embedded;

public static class AxonServerExtensions
{
    public static async Task PurgeEventsAsync(this IAxonServer server)
    {
        await using var connection = new AxonServerConnection(Context.Admin, AxonServerConnectorOptions
            .For(ComponentName.Default)
            .WithRoutingServers(server.GetGrpcEndpoint())
            .Build());
        await connection.WaitUntilConnectedAsync();
        
        //Delete the context
        await connection.AdminChannel.DeleteContextAsync(new DeleteContextRequest
        {
            Name = Context.Default.ToString(),
            PreserveEventStore = false
        });
        
        //Wait for the context to be deleted
        var deleted = false;
        while (!deleted)
        {
            var contexts = await connection.AdminChannel.GetAllContextsAsync();
            deleted = contexts.All(context => context.Name != Context.Default.ToString());
        }
        
        //Recreate the context
        await connection.AdminChannel.CreateContextAsync(new CreateContextRequest
        {
            Name = Context.Default.ToString(),
            ReplicationGroupName = Context.Default.ToString()
        });
        
        //Wait for the context to be created
        var created = false;
        while (!created)
        {
            var contexts = await connection.AdminChannel.GetAllContextsAsync();
            created = contexts.Any(context => context.Name == Context.Default.ToString());
        }

        await Task.Delay(1000);
    }
}