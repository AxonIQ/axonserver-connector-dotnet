namespace AxonIQ.AxonServer.Connector;

public interface IAxonServerConnection : IAsyncDisposable
{
    /// <summary>
    /// Provides access to the admin channel set up with Axon Server.
    /// </summary>
    IAdminChannel AdminChannel { get; }
    /// <summary>
    /// Provides access to the control channel set up with Axon Server.
    /// </summary>
    IControlChannel ControlChannel { get; }
    /// <summary>
    /// Provides access to the command channel set up with Axon Server.
    /// </summary>
    ICommandChannel CommandChannel { get; }
    /// <summary>
    /// Provides access to the query channel set up with Axon Server.
    /// </summary>
    IQueryChannel QueryChannel { get; }
    /// <summary>
    /// Provides access to the event channel set up with Axon Server.
    /// </summary>
    IEventChannel EventChannel { get; }
    /// <summary>
    /// Requests to establish a connection with Axon Server.
    /// </summary>
    /// <returns>A <see cref="Task"/> that confirms the request to connect has been accepted.</returns>
    Task Connect();

    /// <summary>
    /// Waits until a connection with Axon Server has been established.
    /// </summary>
    /// <returns>A <see cref="Task"/> that confirms a connection has been established.</returns>
    Task WaitUntilConnected();

    /// <summary>
    /// Waits until a connection with Axon Server has been established or until the timeout has been reached.
    /// </summary>
    /// <returns>A <see cref="Task"/> that confirms a connection has been established or faulted with a <see cref="TimeoutException"/>.</returns>
    Task WaitUntilConnected(TimeSpan timeout) => WaitUntilConnected().WaitAsync(timeout);
    
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
    
    /// <summary>
    /// Waits until a connection is able to send and receive instructions.
    /// </summary>
    /// <returns>A <see cref="Task"/> that confirms a connection is able to send and receive instructions.</returns>
    Task WaitUntilReady();
    
    /// <summary>
    /// Waits until a connection is able to send and receive instructions or until the timeout has been reached.
    /// </summary>
    /// <returns>A <see cref="Task"/> that confirms a connection is able to send and receive instructions or faulted with a <see cref="TimeoutException"/>.</returns>
    Task WaitUntilReady(TimeSpan timeout) => WaitUntilReady().WaitAsync(timeout);
}