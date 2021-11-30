namespace AxonIQ.AxonServer.Connector;

public static class AxonServerAuthentication
{
    public static readonly IAxonServerAuthentication None = new NoServerAuthentication();
    
    public static IAxonServerAuthentication UsingToken(string token) => new TokenBasedServerAuthentication(token);
}