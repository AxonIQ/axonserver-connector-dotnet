using System.Net;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public interface IAxonCluster : IAsyncLifetime
{
    IReadOnlyList<IAxonClusterNode> Nodes { get; }
    
    IReadOnlyList<Context> Contexts { get; }

    IReadOnlyList<DnsEndPoint> GetHttpEndpoints();

    IReadOnlyList<DnsEndPoint> GetGrpcEndpoints();
}