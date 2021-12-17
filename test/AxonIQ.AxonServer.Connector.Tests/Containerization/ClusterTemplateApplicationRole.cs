using YamlDotNet.RepresentationModel;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class ClusterTemplateApplicationRole
{
    public string? Context { get; set; }
    public string[]? Roles { get; set; }

    public YamlNode Serialize()
    {
        var node = new YamlMappingNode();
        if (!string.IsNullOrEmpty(Context))
        {
            node.Add("context", Context);
        }

        if (Roles != null && Roles.Length != 0)
        {
            node.Add("roles", new YamlSequenceNode(Roles.Select(role => new YamlScalarNode(role))));
        }

        return node;
    }
}