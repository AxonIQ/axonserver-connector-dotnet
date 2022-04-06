namespace AxonIQ.AxonServer.Connector;

public interface IQueryHandlerRegistration : IAsyncDisposable
{
    Task WaitUntilCompleted();
}