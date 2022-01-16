namespace AxonIQ.AxonServer.Connector;

public class AxonServerException : Exception
{
    public AxonServerException(ClientIdentity clientIdentity, ErrorCategory errorCategory, string message): base(message)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        ErrorCategory = errorCategory ?? throw new ArgumentNullException(nameof(errorCategory));
    }

    public AxonServerException(ClientIdentity clientIdentity, ErrorCategory errorCategory, string message, Exception innerException): base(message, innerException)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        ErrorCategory = errorCategory ?? throw new ArgumentNullException(nameof(errorCategory));
    }
    
    public ClientIdentity ClientIdentity { get; }
    public ErrorCategory ErrorCategory { get; }
}