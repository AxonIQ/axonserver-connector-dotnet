using YamlDotNet.RepresentationModel;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class ClusterTemplate
{
    public string? First { get; set; }
    public ClusterTemplateReplicationGroup[]? ReplicationGroups { get; set; }
    public ClusterTemplateApplication[]? Applications { get; set; }
    public ClusterTemplateUser[]? Users { get; set; }

    public YamlDocument Serialize()
    {
        var node = new YamlMappingNode();
        if (!string.IsNullOrEmpty(First))
        {
            node.Add("first", First);
        }
        if (ReplicationGroups != null && ReplicationGroups.Length != 0)
        {
            node.Add("replicationGroups", new YamlSequenceNode(ReplicationGroups.Select(replicationGroup => replicationGroup.Serialize())));
        }
        if (Applications != null && Applications.Length != 0)
        {
            node.Add("applications", new YamlSequenceNode(Applications.Select(application => application.Serialize())));
        }
        if (Users != null && Users.Length != 0)
        {
            node.Add("users", new YamlSequenceNode(Users.Select(user => user.Serialize())));
        }
        return new YamlDocument(
            new YamlMappingNode
            {
                {
                    "axoniq", new YamlMappingNode
                    {
                        {
                            "axonserver", new YamlMappingNode
                            {
                                {
                                    "cluster-template", node
                                }
                            }
                        }
                    }
                }
            }
        );
    }
}