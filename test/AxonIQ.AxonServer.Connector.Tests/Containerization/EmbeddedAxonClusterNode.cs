using shortid.Configuration;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class EmbeddedAxonClusterNode : IAxonClusterNode, IDisposable
{
    public EmbeddedAxonClusterNode(SystemProperties properties)
    {
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        Files = new DirectoryInfo(
            Path.Combine(Path.GetTempPath(), shortid.ShortId.Generate(new GenerationOptions
            {
                UseSpecialCharacters = false
            })));
    }

    public SystemProperties Properties { get; }
    public DirectoryInfo Files { get; }
    
    public void Dispose()
    {
        Files.Delete(true);
    }
}