namespace Benchmarks.Framework;

public interface IBenchmark
{
    string Name { get; }
    
    Task SetupAsync();

    Task RunAsync(); 
        
    Task TeardownAsync();
}