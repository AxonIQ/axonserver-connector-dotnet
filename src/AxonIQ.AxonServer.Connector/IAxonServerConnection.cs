namespace AxonIQ.AxonServer.Connector;

public interface IAxonServerConnection : IAsyncDisposable
{
    /// <summary>
    /// Provides access to the control channel set up with Axon Server.
    /// </summary>
    IControlChannel ControlChannel { get; }
    /// <summary>
    /// Requests to establish a connection with Axon Server.
    /// </summary>
    /// <returns>A <see cref="Task"/> that confirms the request to connect has been accepted.</returns>
    Task Connect();
    /// <summary>
    /// Raised when the connection with Axon Server was established. 
    /// </summary>
    event EventHandler? Connected;
    /// <summary>
    /// Raised when the connection with Axon Server is no longer established. 
    /// </summary>
    event EventHandler? Disconnected;
    /// <summary>
    /// Indicates if the connection with Axon Server is established.
    /// </summary>
    bool IsConnected { get; }
    /// <summary>
    /// Raised when the connection is able to send and receive instructions. 
    /// </summary>
    event EventHandler? Ready;
    /// <summary>
    /// Indicates if the connection is able to send and receive instructions.
    /// </summary>
    bool IsReady { get; }
}