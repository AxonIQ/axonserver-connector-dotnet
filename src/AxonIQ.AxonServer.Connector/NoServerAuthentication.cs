using Grpc.Core;

namespace AxonIQ.AxonServer.Connector;

internal class NoServerAuthentication : IAxonServerAuthentication
{
    public void WriteTo(Metadata metadata)
    {
        //TODO: Write an integration test that sends a request without an access token and one with an empty access token
    }
}