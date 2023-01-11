using AxonIQ.AxonServer.Connector;
using Benchmarks.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Benchmarks;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder().ConfigureLogging(builder => builder.SetMinimumLevel(LogLevel.Debug).AddConsole()).Build();
        
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddGrpcClientInstrumentation()
            .AddHttpClientInstrumentation()
            .AddAxonServerConnectorInstrumentation()
            .AddBenchmarksInstrumentation()
            .AddZipkinExporter()
            .Build();
        
        var benchmarks = new IBenchmark[]
        {
            // new PingPongCommandBenchmark(1),
            // new PingPongCommandBenchmark(10),
            // new PingPongCommandBenchmark(100),
            // new PingPongCommandBenchmark(1000),
            // new PingPongBenchmark(10000),
            // new ParallelPingPongCommandBenchmark(100, 10),
            // new ParallelPingPongCommandBenchmark(1000, 50),
            new PingDotNetPongJavaCommandInteropBenchmark(5000, host.Services.GetService<ILoggerFactory>())
        };

        var runner = new BenchmarkRunner();
        
        foreach (var benchmark in benchmarks)
        {
            await runner.RunAsync(benchmark);
        }
    }
}