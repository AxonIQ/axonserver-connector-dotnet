using YamlDotNet.RepresentationModel;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class ClusterTemplateReplicationGroupRole
{
    public string? Node { get; set; }
    public string? Role { get; set; }

    public YamlNode Serialize()
    {
        var node = new YamlMappingNode();
        if (!string.IsNullOrEmpty(Role))
        {
            node.Add("role", Role);
        }
        
        if (!string.IsNullOrEmpty(Node))
        {
            node.Add("node", Node);
        }

        return node;
    }
}