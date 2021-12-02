using Grpc.Core;

namespace AxonIQ.AxonServer.Connector;

internal static class MetadataExtensions
{
    public static Metadata Clone(this Metadata metadata)
    {
        var clone = new Metadata();
        foreach (var entry in metadata)
        {
            clone.Add(entry);
        }

        return clone;
    }
}