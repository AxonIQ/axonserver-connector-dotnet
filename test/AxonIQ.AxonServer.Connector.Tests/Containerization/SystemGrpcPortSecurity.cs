namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class SystemGrpcPortSecurity
{
    /// <summary>
    /// Determines whether the server has ssl enabled on the gRPC port. Default value is false.
    /// </summary>
    public bool? SslEnabled { get; set; }
    /// <summary>
    /// Location of the public certificate file. Default value is none.
    /// </summary>
    public string? SslCertChainFile { get; set; }
    /// <summary>
    /// Location of the private key file. Default value is none.
    /// </summary>
    public string? SslPrivateKeyFile { get; set; }
    /// <summary>
    /// File containing the full certificate chain to be used in internal communication between Axon Server nodes. If not specified, Axon Server will use the primary key file from ssl.cert-chain-file (Axon EE only). Default value is none.
    /// </summary>
    public string? SslInternalCertChainFile { get; set; }
    /// <summary>
    /// Trusted certificates for verifying the other AxonServer's certificate (Axon EE only). Default value is none.
    /// </summary>
    public string? SslInternalTrustManagerFile { get; set; }
    /// <summary>
    /// File containing the private key to be used in internal communication between Axon Server nodes. If not specified, Axon Server will use the primary key file from ssl.private-key-file (Axon EE only). Default value is none.
    /// </summary>
    public string? SslInternalPrivateKeyFile { get; set; }
    
    public string[] Serialize()
    {
        var properties = new List<string>();
        if (SslEnabled.HasValue)
        {
            properties.Add($"axoniq.axonserver.ssl.enabled={SslEnabled.Value.ToString().ToLowerInvariant()}");
        }

        if (!string.IsNullOrEmpty(SslCertChainFile))
        {
            properties.Add($"axoniq.axonserver.ssl.cert-chain-file={SslCertChainFile}");
        }
        
        if (!string.IsNullOrEmpty(SslPrivateKeyFile))
        {
            properties.Add($"axoniq.axonserver.ssl.private-key-file={SslPrivateKeyFile}");
        }
        
        if (!string.IsNullOrEmpty(SslInternalCertChainFile))
        {
            properties.Add($"axoniq.axonserver.ssl.internal-cert-chain-file={SslInternalCertChainFile}");
        }
        
        if (!string.IsNullOrEmpty(SslInternalTrustManagerFile))
        {
            properties.Add($"axoniq.axonserver.ssl.internal-trust-manager-file={SslInternalTrustManagerFile}");
        }
        
        if (!string.IsNullOrEmpty(SslInternalPrivateKeyFile))
        {
            properties.Add($"axoniq.axonserver.ssl.internal-private-key-file={SslInternalPrivateKeyFile}");
        }

        return properties.ToArray();
    }

    public void CopyTo(SystemGrpcPortSecurity other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        other.SslEnabled = SslEnabled;
        other.SslCertChainFile = SslCertChainFile;
        other.SslPrivateKeyFile = SslPrivateKeyFile;
        other.SslInternalCertChainFile = SslInternalCertChainFile;
        other.SslInternalPrivateKeyFile = SslInternalPrivateKeyFile;
        other.SslInternalTrustManagerFile = SslInternalTrustManagerFile;
    }
}