using System.Runtime.CompilerServices;
using Grpc.Core;

namespace AxonIQ.AxonServer.Connector;

internal static class AsyncReaderExtensions
{
    public static ValueTask<List<TSource>> ToListAsync<TSource>(
        this IAsyncStreamReader<TSource> reader,
        CancellationToken cancellationToken = default)
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));
        
        
        return ToListAsyncCore(reader, cancellationToken);
    }

    private static async ValueTask<List<T>> ToListAsyncCore<T>(IAsyncStreamReader<T> reader, CancellationToken cancellationToken)
    {
        var list = new List<T>();
        while (await reader.MoveNext(cancellationToken).ConfigureAwait(false))
        {
            list.Add(reader.Current);
        }

        return list;
    }
}