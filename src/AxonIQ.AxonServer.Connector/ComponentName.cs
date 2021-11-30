using System.Diagnostics.Contracts;

namespace AxonIQ.AxonServer.Connector;

public readonly struct ComponentName
{
    private readonly string _value;

    public ComponentName(string value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (value == string.Empty) throw new ArgumentException("The component name can not be empty", nameof(value));
        _value = value;
    }

    private bool Equals(ComponentName instance) => instance._value.Equals(_value);
    public override bool Equals(object? obj) => obj is ComponentName instance && instance.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString()
    {
        return _value;
    }

    [Pure]
    public ComponentName SuffixWith(string suffix)
    {
        return SuffixWith(new ComponentName(suffix));
    }
    
    [Pure]
    public ComponentName SuffixWith(ComponentName suffix)
    {
        return new ComponentName(_value + suffix._value);
    }
    
    [Pure]
    public ComponentName PrefixWith(string prefix)
    {
        return PrefixWith(new ComponentName(prefix));
    }

    [Pure]
    public ComponentName PrefixWith(ComponentName prefix)
    {
        return new ComponentName(prefix._value + _value);
    }
    
    private static readonly char[] HexCharacters = {
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
        'a', 'b', 'c', 'd', 'e', 'f'
    };

    internal static ComponentName GenerateRandom(int length)
    {
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length), length, "The component name length can not be negative or zero");
        return new ComponentName(
            new string(
                Enumerable
                    .Range(0, length)
                    .Select(_ => HexCharacters[Random.Shared.Next(0, 16)])
                    .ToArray()));
    }
}