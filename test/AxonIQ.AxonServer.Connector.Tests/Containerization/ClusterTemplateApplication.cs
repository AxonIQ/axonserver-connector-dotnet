using YamlDotNet.RepresentationModel;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class ClusterTemplateApplication
{
    public string? Token { get; set; }
    public ClusterTemplateApplicationRole[]? Roles { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public KeyValuePair<string, string>[]? Metadata { get; set; }
    
    public YamlNode Serialize()
    {
        var node = new YamlMappingNode();
        if (!string.IsNullOrEmpty(Token))
        {
            node.Add("token", Token);
        }

        if (Roles != null && Roles.Length != 0)
        {
            node.Add("roles", new YamlSequenceNode(Roles.Select(role => role.Serialize())));
        }
        
        if (!string.IsNullOrEmpty(Name))
        {
            node.Add("name", Name);
        }

        if (!string.IsNullOrEmpty(Description))
        {
            node.Add("description", Description);
        }
        
        if (Metadata != null && Metadata.Length != 0)
        {
            node.Add("metaData",
                new YamlMappingNode(Metadata.Select(metadatum =>
                    new KeyValuePair<YamlNode, YamlNode>(
                        new YamlScalarNode(metadatum.Key),
                        new YamlScalarNode(metadatum.Value))))
            );
        }
        
        return node;
    }
}