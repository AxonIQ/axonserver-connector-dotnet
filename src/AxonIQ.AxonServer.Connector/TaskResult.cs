namespace AxonIQ.AxonServer.Connector;

internal abstract record TaskResult
{
    public record Ok : TaskResult;
    public record Error(Exception Exception) : TaskResult;
}