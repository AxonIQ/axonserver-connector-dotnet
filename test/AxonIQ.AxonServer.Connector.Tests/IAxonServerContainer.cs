using Grpc.Net.Client;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public interface IAxonServerContainer : IAsyncLifetime
{
    HttpClient CreateHttpClient();

    GrpcChannel CreateGrpcChannel();
}