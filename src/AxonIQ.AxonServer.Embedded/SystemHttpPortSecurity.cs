namespace AxonIQ.AxonServer.Embedded;

public class SystemHttpPortSecurity
{
    /// <summary>
    /// Determines whether the server has ssl enabled on the HTTP port. Default value is false.
    /// </summary>
    public bool? SecurityRequireSsl { get; set; }
    /// <summary>
    /// Keystore type (should be PKCS12). Default value is none.
    /// </summary>
    public string? ServerSslKeyStoreType { get; set; }
    /// <summary>
    /// Location of the keystore. Default value is none.
    /// </summary>
    public string? ServerSslKeyStore { get; set; }
    /// <summary>
    /// Password to access the keystore. Default value is none.
    /// </summary>
    public string? ServerSslKeyStorePassword { get; set; }
    /// <summary>
    /// Alias to be used to access the keystore. Default value is none.
    /// </summary>
    public string? ServerSslKeyAlias { get; set; }
    
    public string[] Serialize()
    {
        var properties = new List<string>();
        if (SecurityRequireSsl.HasValue)
        {
            properties.Add($"security.require-ssl={SecurityRequireSsl.Value.ToString().ToLowerInvariant()}");
        }

        if (!string.IsNullOrEmpty(ServerSslKeyStoreType))
        {
            properties.Add($"server.ssl.key-store-type={ServerSslKeyStoreType}");
        }
        
        if (!string.IsNullOrEmpty(ServerSslKeyStore))
        {
            properties.Add($"server.ssl.key-store={ServerSslKeyStore}");
        }
        
        if (!string.IsNullOrEmpty(ServerSslKeyStorePassword))
        {
            properties.Add($"server.ssl.key-store-password={ServerSslKeyStorePassword}");
        }
        
        if (!string.IsNullOrEmpty(ServerSslKeyAlias))
        {
            properties.Add($"server.ssl.key-alias={ServerSslKeyAlias}");
        }

        return properties.ToArray();
    }

    public void CopyTo(SystemHttpPortSecurity other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        other.SecurityRequireSsl = SecurityRequireSsl;
        other.ServerSslKeyAlias = ServerSslKeyAlias;
        other.ServerSslKeyStore = ServerSslKeyStore;
        other.ServerSslKeyStorePassword = ServerSslKeyStorePassword;
        other.ServerSslKeyStoreType = ServerSslKeyStoreType;
    }
}