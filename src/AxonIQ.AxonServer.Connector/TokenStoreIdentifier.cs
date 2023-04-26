namespace AxonIQ.AxonServer.Connector;

public readonly struct TokenStoreIdentifier : IEquatable<TokenStoreIdentifier>
{
    public static readonly TokenStoreIdentifier Empty = new("");
    
    private readonly string _value;

    public TokenStoreIdentifier(string value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public bool Equals(TokenStoreIdentifier other) => string.Equals(_value, other._value);
    public override bool Equals(object? obj) => obj is TokenStoreIdentifier other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString() => _value;

    public static bool operator ==(TokenStoreIdentifier left, TokenStoreIdentifier right) => left.Equals(right);
    public static bool operator !=(TokenStoreIdentifier left, TokenStoreIdentifier right) => !(left == right);
}