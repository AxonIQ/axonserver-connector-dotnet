using YamlDotNet.RepresentationModel;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class ClusterTemplateUserRole
{
    public string[]? Roles { get; set; }
    public string? Context { get; set; }

    public YamlNode Serialize()
    {
        var node = new YamlMappingNode();
        if (Roles != null && Roles.Length != 0)
        {
            node.Add("roles", new YamlSequenceNode(Roles.Select(role => new YamlScalarNode(role))));
        }
        
        if (!string.IsNullOrEmpty(Context))
        {
            node.Add("context", Context);
        }

        return node;
    }
}