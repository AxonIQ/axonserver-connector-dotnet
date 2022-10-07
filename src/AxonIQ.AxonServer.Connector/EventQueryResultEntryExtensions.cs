namespace AxonIQ.AxonServer.Connector;

public static class EventQueryResultEntryExtensions
{
    public static string? GetValueAsNullableString(this IEventQueryResultEntry entry, string column) =>
        entry.GetValueAsNullable<string>(column);
    public static long? GetValueAsNullableInt64(this IEventQueryResultEntry entry, string column) =>
        entry.GetValueAsNullable<long>(column);
    public static double? GetValueAsNullableDouble(this IEventQueryResultEntry entry, string column) =>
        entry.GetValueAsNullable<double>(column);
    public static bool? GetValueAsNullableBoolean(this IEventQueryResultEntry entry, string column) =>
        entry.GetValueAsNullable<bool>(column);
    
    public static string GetValueAsString(this IEventQueryResultEntry entry, string column) =>
        entry.GetValueOrDefaultAs<string>(column);
    public static long GetValueAsInt64(this IEventQueryResultEntry entry, string column) =>
        entry.GetValueOrDefaultAs<long>(column);
    public static double GetValueAsDouble(this IEventQueryResultEntry entry, string column) =>
        entry.GetValueOrDefaultAs<double>(column);
    public static bool GetValueAsBoolean(this IEventQueryResultEntry entry, string column) =>
        entry.GetValueOrDefaultAs<bool>(column);
}