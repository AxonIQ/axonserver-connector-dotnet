using YamlDotNet.RepresentationModel;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class ClusterTemplateReplicationGroup
{
    public string? Name { get; set; }
    public ClusterTemplateReplicationGroupRole[]? Roles { get; set; }
    public ClusterTemplateReplicationGroupContext[]? Contexts { get; set; }

    public YamlNode Serialize()
    {
        var node = new YamlMappingNode();
        if (!string.IsNullOrEmpty(Name))
        {
            node.Add("name", Name);
        }

        if (Roles != null && Roles.Length != 0)
        {
            node.Add("roles", new YamlSequenceNode(Roles.Select(role => role.Serialize())));
        }

        if (Contexts != null && Contexts.Length != 0)
        {
            node.Add("contexts", new YamlSequenceNode(Contexts.Select(context => context.Serialize())));
        }

        return node;
    }
}