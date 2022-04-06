namespace AxonIQ.AxonServer.Connector;

public interface ICommandHandlerRegistration : IAsyncDisposable
{
    Task WaitUntilCompleted();
}