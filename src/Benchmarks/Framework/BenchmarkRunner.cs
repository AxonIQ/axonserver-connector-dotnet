using System.Diagnostics;

namespace Benchmarks.Framework;

public class BenchmarkRunner : IBenchmarkRunner
{
    public async Task RunAsync(IBenchmark benchmark)
    {
        using var activity = Telemetry.Source.StartActivity(benchmark.Name);

        using (Telemetry.Source.StartActivity("setup"))
        {
            await benchmark.SetupAsync();    
        }
        try
        {
            using (Telemetry.Source.StartActivity("run"))
            {
                await benchmark.RunAsync();
            }
        }
        finally
        {
            using (Telemetry.Source.StartActivity("teardown"))
            {
                await benchmark.TeardownAsync();
            }    
        }
    }
}