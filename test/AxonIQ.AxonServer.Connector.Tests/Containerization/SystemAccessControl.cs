namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class SystemAccessControl
{
    /// <summary>
    /// Indicates that access control is enabled for the server. Default value is false.
    /// </summary>
    public bool? AccessControlEnabled { get; set; }

    /// <summary>
    /// Timeout for authenticated tokens. Default value is 300000.
    /// </summary>
    public int? AccessControlCacheTtl { get; set; }

    /// <summary>
    /// Token used to authenticate Axon Server instances in a cluster (Axon EE only).
    /// </summary>
    public string? AccessControlInternalToken { get; set; }

    /// <summary>
    /// Token to be used by client applications connecting to Axon Server (Axon SE only).
    /// </summary>
    public string? AccessControlToken { get; set; }

    /// <summary>
    /// Token to be used by CLI to manage Admin Server users (Axon SE only)
    /// </summary>
    public string? AccessControlAdminToken { get; set; }

    /// <summary>
    /// File containing a predefined system token.
    /// </summary>
    public string? AccessControlSystemToken { get; set; }

    public string[] Serialize()
    {
        var properties = new List<string>();
        if (AccessControlEnabled.HasValue)
        {
            properties.Add(
                $"axoniq.axonserver.accesscontrol.enabled={AccessControlEnabled.Value.ToString().ToLowerInvariant()}");
        }

        if (AccessControlCacheTtl.HasValue)
        {
            properties.Add($"axoniq.axonserver.accesscontrol.cache-ttl={AccessControlCacheTtl.Value}");
        }

        if (!string.IsNullOrEmpty(AccessControlInternalToken))
        {
            properties.Add($"axoniq.axonserver.accesscontrol.internal-token={AccessControlInternalToken}");
        }

        if (!string.IsNullOrEmpty(AccessControlToken))
        {
            properties.Add($"axoniq.axonserver.accesscontrol.token={AccessControlToken}");
        }

        if (!string.IsNullOrEmpty(AccessControlAdminToken))
        {
            properties.Add($"axoniq.axonserver.accesscontrol.admin-token={AccessControlAdminToken}");
        }

        if (!string.IsNullOrEmpty(AccessControlSystemToken))
        {
            properties.Add($"axoniq.axonserver.accesscontrol.system-token={AccessControlSystemToken}");
        }

        return properties.ToArray();
    }

    public void CopyTo(SystemAccessControl other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        other.AccessControlEnabled = AccessControlEnabled;
        other.AccessControlToken = AccessControlToken;
        other.AccessControlAdminToken = AccessControlAdminToken;
        other.AccessControlCacheTtl = AccessControlCacheTtl;
        other.AccessControlInternalToken = AccessControlInternalToken;
        other.AccessControlSystemToken = AccessControlSystemToken;
    }
}