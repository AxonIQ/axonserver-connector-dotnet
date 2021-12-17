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
            node.Add("replicationGroups",
                new YamlSequenceNode(ReplicationGroups.Select(replicationGroup => replicationGroup.Serialize())));
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

    public Context[] ScanForContexts()
    {
        var contexts = new HashSet<Context>();
        
        if (Applications != null)
        {
            foreach (var application in Applications)
            {
                if (application.Roles != null)
                {
                    foreach (var role in application.Roles)
                    {
                        if (!string.IsNullOrEmpty(role.Context))
                        {
                            contexts.Add(new Context(role.Context));
                        }
                    }
                }
            }
        }

        if (ReplicationGroups != null)
        {
            foreach (var replicationGroup in ReplicationGroups)
            {
                if (replicationGroup.Contexts != null)
                {
                    foreach (var context in replicationGroup.Contexts)
                    {
                        if (!string.IsNullOrEmpty(context.Name))
                        {
                            contexts.Add(new Context(context.Name));
                        }
                    }
                }
            }
        }

        if (Users != null)
        {
            foreach (var user in Users)
            {
                if (user.Roles != null)
                {
                    foreach (var role in user.Roles)
                    {
                        if (!string.IsNullOrEmpty(role.Context))
                        {
                            contexts.Add(new Context(role.Context));
                        }
                    }
                }
            }
        }

        return contexts.ToArray();
    }
}