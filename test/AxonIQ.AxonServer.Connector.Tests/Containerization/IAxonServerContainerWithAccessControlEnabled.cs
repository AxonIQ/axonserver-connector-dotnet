namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public interface IAxonServerContainerWithAccessControlEnabled : IAxonServerContainer
{
    string Token { get; }
}