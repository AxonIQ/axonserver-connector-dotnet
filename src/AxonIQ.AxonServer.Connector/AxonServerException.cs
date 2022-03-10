using Google.Protobuf.Reflection;

namespace AxonIQ.AxonServer.Connector;

public class AxonServerException : Exception
{
    public AxonServerException(ClientIdentity clientIdentity, ErrorCategory errorCategory, string message): base(message)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        ErrorCategory = errorCategory ?? throw new ArgumentNullException(nameof(errorCategory));
        Location = string.Empty;
        Details = Array.Empty<string>();
    }
    
    public AxonServerException(ClientIdentity clientIdentity, ErrorCategory errorCategory, string message, string location): base(message)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        ErrorCategory = errorCategory ?? throw new ArgumentNullException(nameof(errorCategory));
        Location = location ?? throw new ArgumentNullException(nameof(location));
        Details = Array.Empty<string>();
    }
    
    public AxonServerException(ClientIdentity clientIdentity, ErrorCategory errorCategory, string message, string location, IReadOnlyCollection<string> details): base(message)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        ErrorCategory = errorCategory ?? throw new ArgumentNullException(nameof(errorCategory));
        Location = location ?? throw new ArgumentNullException(nameof(location));
        Details = details ?? throw new ArgumentNullException(nameof(details));
    }

    public AxonServerException(ClientIdentity clientIdentity, ErrorCategory errorCategory, string message, Exception innerException): base(message, innerException)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        ErrorCategory = errorCategory ?? throw new ArgumentNullException(nameof(errorCategory));
        Location = string.Empty;
        Details = Array.Empty<string>();
    }
    
    public AxonServerException(ClientIdentity clientIdentity, ErrorCategory errorCategory, string message, string location, Exception innerException): base(message, innerException)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        ErrorCategory = errorCategory ?? throw new ArgumentNullException(nameof(errorCategory));
        Location = location ?? throw new ArgumentNullException(nameof(location));
        Details = Array.Empty<string>();
    }
    
    public AxonServerException(ClientIdentity clientIdentity, ErrorCategory errorCategory, string message, string location, IReadOnlyCollection<string> details, Exception innerException): base(message, innerException)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        ErrorCategory = errorCategory ?? throw new ArgumentNullException(nameof(errorCategory));
        Location = location ?? throw new ArgumentNullException(nameof(location));
        Details = details ?? throw new ArgumentNullException(nameof(details));
    }
    
    public ClientIdentity ClientIdentity { get; }
    public ErrorCategory ErrorCategory { get; }
    public string Location { get; }
    public IReadOnlyCollection<string> Details { get; }
}