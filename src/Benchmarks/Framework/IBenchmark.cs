namespace Benchmarks.Framework;

public interface IBenchmark
{
    string Name { get; }
    
    Task InitializeAsync();

    Task RunAsync(); 
        
    Task DisposeAsync();
}