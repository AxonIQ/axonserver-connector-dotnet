namespace AxonIQ.AxonServer.Connector;

// inlined from System.Linq.Async
internal static class AsyncEnumerable
{
    public static ValueTask<List<TSource>> ToListAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        return Core(source, cancellationToken);

        static async ValueTask<List<TSource>> Core(IAsyncEnumerable<TSource> source, CancellationToken cancellationToken)
        {
            var list = new List<TSource>();

            await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                list.Add(item);
            }

            return list;
        }
    }
}