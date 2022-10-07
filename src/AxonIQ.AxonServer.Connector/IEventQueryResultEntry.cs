namespace AxonIQ.AxonServer.Connector;

public interface IEventQueryResultEntry
{
    IReadOnlyCollection<string> Columns { get; }
    IReadOnlyCollection<object?> Identifiers { get; }
    IReadOnlyCollection<object?> SortValues { get; }
    T? GetValueAsNullable<T>(string column);
    T GetValueOrDefaultAs<T>(string column, T? defaultValue = default);
}