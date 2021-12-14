using YamlDotNet.RepresentationModel;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class ClusterTemplateUserRole
{
    public string? Role { get; set; }
    public string? Context { get; set; }

    public YamlNode Serialize()
    {
        var node = new YamlMappingNode();
        if (!string.IsNullOrEmpty(Role))
        {
            node.Add("role", Role);
        }
        
        if (!string.IsNullOrEmpty(Context))
        {
            node.Add("context", Context);
        }

        return node;
    }
}