using System.Diagnostics;

namespace Benchmarks.Framework;

public class BenchmarkRunner : IBenchmarkRunner
{
    public async Task<IReadOnlyCollection<Trace>> RunAsync(IBenchmark benchmark)
    {
        var traces = new List<Trace>();
        
        var watch = Stopwatch.StartNew();
        await benchmark.InitializeAsync();
        traces.Add(new Trace($"{benchmark.Name}.Initialize", watch.Elapsed));
        
        watch.Restart();
        await benchmark.RunAsync();
        traces.Add(new Trace($"{benchmark.Name}.Run", watch.Elapsed));
        
        watch.Restart();
        await benchmark.DisposeAsync();
        traces.Add(new Trace($"{benchmark.Name}.Dispose", watch.Elapsed));

        return traces;
    }
}