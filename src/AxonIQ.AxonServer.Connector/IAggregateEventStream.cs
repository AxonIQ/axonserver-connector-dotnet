using Io.Axoniq.Axonserver.Grpc.Event;

namespace AxonIQ.AxonServer.Connector;

public interface IAggregateEventStream : IAsyncEnumerable<Event>, IDisposable
{
}