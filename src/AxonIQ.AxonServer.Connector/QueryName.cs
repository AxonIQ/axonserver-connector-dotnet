namespace AxonIQ.AxonServer.Connector;

public readonly struct QueryName : IEquatable<QueryName>
{
    private readonly string _value;

    public QueryName(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        
        if (value == "")
        {
            throw new ArgumentException("The query name can not be empty.", nameof(value));
        }

        _value = value;
    }
    
    public bool Equals(QueryName other) => other._value.Equals(_value);
    public override bool Equals(object? obj) => obj is QueryName other && other.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString() => _value;

    public static bool operator ==(QueryName left, QueryName right) => left.Equals(right);
    public static bool operator !=(QueryName left, QueryName right) => !left.Equals(right);
}