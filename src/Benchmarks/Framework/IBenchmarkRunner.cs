namespace Benchmarks.Framework;

public interface IBenchmarkRunner
{
    Task RunAsync(IBenchmark benchmark);
}