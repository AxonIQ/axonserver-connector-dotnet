using Grpc.Core;

namespace AxonIQ.AxonServer.Connector;

internal class TokenBasedServerAuthentication : IAxonServerAuthentication
{
    public TokenBasedServerAuthentication(string token)
    {
        Token = token ?? throw new ArgumentNullException(nameof(token));
    }

    public string Token { get; }

    public void WriteTo(Metadata metadata)
    {
        if (metadata == null) throw new ArgumentNullException(nameof(metadata));
        metadata.Add(AxonServerConnectionHeaders.AccessToken, Token);
    }
}