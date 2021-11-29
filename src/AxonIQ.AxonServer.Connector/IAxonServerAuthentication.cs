using Grpc.Core;

namespace AxonIQ.AxonServer.Connector;

public interface IAxonServerAuthentication
{
    void WriteTo(Metadata metadata);
}