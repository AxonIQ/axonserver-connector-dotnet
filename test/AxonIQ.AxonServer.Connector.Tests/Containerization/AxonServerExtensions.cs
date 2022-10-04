namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public static class AxonServerExtensions
{
    public static async Task PurgeEvents(this IAxonServer server)
    {
        using var client = server.CreateHttpClient();
        (await client.DeleteAsync("v1/devmode/purge-events")).EnsureSuccessStatusCode();
    }
}