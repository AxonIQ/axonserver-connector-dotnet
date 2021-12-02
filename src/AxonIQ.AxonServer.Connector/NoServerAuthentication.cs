using Grpc.Core;
using Grpc.Core.Interceptors;

namespace AxonIQ.AxonServer.Connector;

internal class NoServerAuthentication : IAxonServerAuthentication
{
    public void WriteTo(Metadata metadata)
    {
        if (metadata == null) throw new ArgumentNullException(nameof(metadata));
    }
}