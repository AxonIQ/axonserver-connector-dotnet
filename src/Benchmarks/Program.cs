using Benchmarks.Framework;

namespace Benchmarks;

public class Program
{
    public static async Task Main(string[] args)
    {
        var benchmarks = new IBenchmark[]
        {
            new PingPongBenchmark(1),
            new PingPongBenchmark(10),
            new PingPongBenchmark(100),
            new PingPongBenchmark(1000),
            new PingPongBenchmark(10000)
        };

        var runner = new BenchmarkRunner();
        
        foreach (var benchmark in benchmarks)
        {
            Console.WriteLine("Running {0}", benchmark.Name);
            var traces = await runner.RunAsync(benchmark);
            foreach (var trace in traces)
            {
                Console.WriteLine(trace.ToString());    
            }
        }
    }
}