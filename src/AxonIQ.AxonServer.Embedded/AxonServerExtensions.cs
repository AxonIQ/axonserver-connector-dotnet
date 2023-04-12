namespace AxonIQ.AxonServer.Embedded;

public static class AxonServerExtensions
{
    public static async Task PurgeEventsAsync(this IAxonServer server)
    {
        using var client = server.CreateHttpClient();
        (await client.DeleteAsync("v1/devmode/purge-events")).EnsureSuccessStatusCode();
    }
}