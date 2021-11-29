using Grpc.Core;

namespace AxonIQ.AxonServer.Connector;

internal class NoServerAuthentication : IAxonServerAuthentication
{
    public void WriteTo(Metadata metadata)
    {
    }
}