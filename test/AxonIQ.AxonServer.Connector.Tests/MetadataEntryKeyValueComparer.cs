using Grpc.Core;

namespace AxonIQ.AxonServer.Connector.Tests;

public class MetadataEntryKeyValueComparer : IEqualityComparer<Metadata.Entry>
{
    public bool Equals(Metadata.Entry? x, Metadata.Entry? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        if (x.GetType() != y.GetType()) return false;
        return x.Key == y.Key && x.Value == y.Value;
    }

    public int GetHashCode(Metadata.Entry obj)
    {
        return HashCode.Combine(obj.Key, obj.Value);
    }
}