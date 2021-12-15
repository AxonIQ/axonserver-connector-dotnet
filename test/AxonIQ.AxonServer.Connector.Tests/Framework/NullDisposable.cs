namespace AxonIQ.AxonServer.Connector.Tests.Framework;

internal class NullDisposable : IDisposable
{
    public static readonly IDisposable Instance = new NullDisposable();

    public void Dispose()
    {
    }
}