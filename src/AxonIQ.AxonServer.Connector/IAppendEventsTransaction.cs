using Io.Axoniq.Axonserver.Grpc.Event;

namespace AxonIQ.AxonServer.Connector;

public interface IAppendEventsTransaction : IDisposable
{
    Task AppendEventAsync(Event @event);

    Task<Confirmation> CommitAsync();

    Task Rollback();
}