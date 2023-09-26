namespace AxonIQ.AxonServer.Connector;

internal static class ReadOnlyCollectionExtensions
{
    public static IReadOnlyCollection<TOutput> Select<TInput, TOutput>(
        this IReadOnlyCollection<TInput> source,
        Func<TInput, TOutput> selector)
    {
        var destination = new TOutput[source.Count];
        var index = 0;
        foreach (var input in source)
        {
            destination[index] = selector(input);
            index++;
        }

        return destination;
    }
}