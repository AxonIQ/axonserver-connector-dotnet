using YamlDotNet.RepresentationModel;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class ClusterTemplateUser
{
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public ClusterTemplateUserRole[]? Roles { get; set; }

    public YamlNode Serialize()
    {
        var node = new YamlMappingNode();
        if (!string.IsNullOrEmpty(UserName))
        {
            node.Add("userName", UserName);
        }
        
        if (!string.IsNullOrEmpty(Password))
        {
            node.Add("password", Password);
        }
        
        if (Roles != null && Roles.Length != 0)
        {
            node.Add("roles", new YamlSequenceNode(Roles.Select(role => role.Serialize())));
        }

        return node;
    }
}