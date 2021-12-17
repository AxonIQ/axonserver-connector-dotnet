using YamlDotNet.RepresentationModel;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class ClusterTemplateReplicationGroupContext
{
    public KeyValuePair<string, string>[]? Metadata { get; set; }
    public string? Name { get; set; }

    public YamlNode Serialize()
    {
        var node = new YamlMappingNode();
        if (!string.IsNullOrEmpty(Name))
        {
            node.Add("name", Name);    
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
        else
        {
            node.Add("metaData", new YamlMappingNode());
        }

        return node;
    }
}