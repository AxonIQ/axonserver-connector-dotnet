using Io.Axoniq.Axonserver.Grpc.Control;

namespace AxonIQ.AxonServer.Connector;

internal static class ClientIdentityExtensions
{
    public static ClientIdentification ToClientIdentification(this ClientIdentity identity)
    {
        var identification = new ClientIdentification
        {
            ComponentName = identity.ComponentName.ToString(),
            ClientId = identity.ClientInstanceId.ToString(),
            Version = identity.Version.ToString()
        };
        foreach (var (key, value) in identity.ClientTags)
        {
            identification.Tags.Add(key, value);
        }

        return identification;
    }
}