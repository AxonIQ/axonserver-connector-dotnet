namespace Benchmarks.Framework;

public interface IBenchmarkRunner
{
    Task<IReadOnlyCollection<Trace>> RunAsync(IBenchmark benchmark);
}