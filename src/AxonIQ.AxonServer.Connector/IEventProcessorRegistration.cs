namespace AxonIQ.AxonServer.Connector;

public interface IEventProcessorRegistration : IAsyncDisposable
{
    Task WaitUntilCompleted();
}