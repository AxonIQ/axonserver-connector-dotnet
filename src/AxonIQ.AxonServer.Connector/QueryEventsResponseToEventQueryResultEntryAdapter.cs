using Io.Axoniq.Axonserver.Grpc.Event;

namespace AxonIQ.AxonServer.Connector;

public class QueryEventsResponseToEventQueryResultEntryAdapter : IEventQueryResultEntry
{
    private readonly QueryEventsResponse _response;

    internal QueryEventsResponseToEventQueryResultEntryAdapter(QueryEventsResponse response, IReadOnlyCollection<string> columns)
    {
        _response = response ?? throw new ArgumentNullException(nameof(response));
        Columns = columns ?? throw new ArgumentNullException(nameof(columns));
    }
    
    public IReadOnlyCollection<string> Columns { get; }

    public IReadOnlyCollection<object?> Identifiers =>
        _response
            .Row
            .IdValues
            .Select<QueryValue, object?>(ToBoxedValue)
            .ToArray();

    public IReadOnlyCollection<object?> SortValues =>
        _response
            .Row
            .SortValues
            .Select<QueryValue, object?>(ToBoxedValue)
            .ToArray();

    private static object? ToBoxedValue(QueryValue value)
    {
        return value.DataCase switch
        {
            QueryValue.DataOneofCase.TextValue => value.TextValue,
            QueryValue.DataOneofCase.NumberValue => value.NumberValue,
            QueryValue.DataOneofCase.BooleanValue => value.BooleanValue,
            QueryValue.DataOneofCase.DoubleValue => value.DoubleValue,
            _ => null
        };
    }
    public T? GetValueAsNullable<T>(string column)
    {
        if (_response.Row.Values.TryGetValue(column, out var value))
        {
            return (T?)ToBoxedValue(value);
        }

        return default;
    }

    public T GetValueOrDefaultAs<T>(string column, T? defaultValue)
    {
        if (_response.Row.Values.TryGetValue(column, out var value))
        {
            return (T?)ToBoxedValue(value) ?? (defaultValue ?? default!);
        }

        return (defaultValue ?? default!);
    }
}