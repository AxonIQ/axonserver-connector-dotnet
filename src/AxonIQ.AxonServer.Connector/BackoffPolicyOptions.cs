namespace AxonIQ.AxonServer.Connector;

public class BackoffPolicyOptions : IEquatable<BackoffPolicyOptions>
{
    public BackoffPolicyOptions(TimeSpan initialBackoff, TimeSpan maximumBackoff, double backoffMultiplier)
    {
        if (initialBackoff < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(initialBackoff), initialBackoff,
                "The initial backoff can not be negative.");
        if (maximumBackoff < initialBackoff)
            throw new ArgumentOutOfRangeException(nameof(maximumBackoff), maximumBackoff,
                "The maximum backoff can not be less than the initial backoff.");
        if (backoffMultiplier < 1.0)
            throw new ArgumentOutOfRangeException(nameof(backoffMultiplier), backoffMultiplier,
                "The backoff multiplier can not be less than one.");
        InitialBackoff = initialBackoff;
        MaximumBackoff = maximumBackoff;
        BackoffMultiplier = backoffMultiplier;
    }

    public TimeSpan InitialBackoff { get; }
    public TimeSpan MaximumBackoff { get; }
    public double BackoffMultiplier { get; }

    public TimeSpan Next(TimeSpan current)
    {
        if (current < InitialBackoff)
            return InitialBackoff;

        if (current > MaximumBackoff)
            return MaximumBackoff;

        var next = current * BackoffMultiplier;
        return next > MaximumBackoff ? MaximumBackoff : next;
    }
    
    public bool Equals(BackoffPolicyOptions? other) =>
        !ReferenceEquals(null, other) && (ReferenceEquals(this, other) ||
                                          InitialBackoff.Equals(other.InitialBackoff) &&
                                          MaximumBackoff.Equals(other.MaximumBackoff) &&
                                          BackoffMultiplier.Equals(other.BackoffMultiplier));

    public override bool Equals(object? obj) =>
        !ReferenceEquals(null, obj) &&
        (ReferenceEquals(this, obj) || obj is BackoffPolicyOptions other && Equals(other));

    public override int GetHashCode() => HashCode.Combine(InitialBackoff, MaximumBackoff, BackoffMultiplier);

    public static bool operator ==(BackoffPolicyOptions? left, BackoffPolicyOptions? right) => Equals(left, right);
    public static bool operator !=(BackoffPolicyOptions? left, BackoffPolicyOptions? right) => !Equals(left, right);
}