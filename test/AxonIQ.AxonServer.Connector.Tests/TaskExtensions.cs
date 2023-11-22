namespace AxonIQ.AxonServer.Connector.Tests;

internal static class TaskExtensions
{
    public static async Task<bool> WaitUntilTimeoutAsync(this Task task, TimeSpan timeout)
    {
        try
        {
            await task.WaitAsync(timeout);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}