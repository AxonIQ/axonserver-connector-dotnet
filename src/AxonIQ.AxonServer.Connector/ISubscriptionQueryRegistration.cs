namespace AxonIQ.AxonServer.Connector;

public interface ISubscriptionQueryRegistration : IAsyncDisposable
{
    Task WaitUntilCompleted();
}