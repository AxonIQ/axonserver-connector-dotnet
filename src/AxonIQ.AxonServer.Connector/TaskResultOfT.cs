namespace AxonIQ.AxonServer.Connector;

internal abstract record TaskResult<T>
{
    public record Ok(T Value) : TaskResult<T>;
    public record Error(Exception Exception) : TaskResult<T>;
}