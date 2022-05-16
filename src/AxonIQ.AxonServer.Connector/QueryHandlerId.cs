namespace AxonIQ.AxonServer.Connector;

public readonly struct QueryHandlerId
{
    public static QueryHandlerId New() => new QueryHandlerId(Guid.NewGuid().ToString("D"));
    
    private readonly string _value;

    public QueryHandlerId(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        
        if (value == "")
        {
            throw new ArgumentException("The query handler identifier can not be empty.", nameof(value));
        }

        _value = value;
    }
    
    public bool Equals(QueryHandlerId other) => other._value.Equals(_value);
    public override bool Equals(object? obj) => obj is QueryHandlerId other && other.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString() => _value;
}