using Io.Axoniq.Axonserver.Grpc.Event;

namespace AxonIQ.AxonServer.Connector;

public interface IEventStream : IAsyncEnumerable<EventWithToken>, IDisposable
{
    Task ExcludePayloadTypeAsync(string payloadType, string? revision);
}