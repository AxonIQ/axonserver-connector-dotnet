namespace AxonIQ.AxonServer.Connector;

public interface IAxonServerConnection : IAsyncDisposable
{
    /// <summary>
    /// Provides access to the admin channel set up with Axon Server.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is closed or disposed.</exception>
    IAdminChannel AdminChannel { get; }
    /// <summary>
    /// Provides access to the control channel set up with Axon Server.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is closed or disposed.</exception>
    IControlChannel ControlChannel { get; }
    /// <summary>
    /// Provides access to the command channel set up with Axon Server.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is closed or disposed.</exception>
    ICommandChannel CommandChannel { get; }
    /// <summary>
    /// Provides access to the query channel set up with Axon Server.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is closed or disposed.</exception>
    IQueryChannel QueryChannel { get; }
    /// <summary>
    /// Provides access to the event channel set up with Axon Server.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is closed or disposed.</exception>
    IEventChannel EventChannel { get; }

    /// <summary>
    /// Indicates if the connection with Axon Server is established.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Waits until a connection with Axon Server has been established.
    /// </summary>
    /// <returns>A <see cref="Task"/> that confirms a connection has been established.</returns>
    /// <exception cref="TaskCanceledException">Thrown if this instance is closed or disposed.</exception>
    Task WaitUntilConnectedAsync();

    /// <summary>
    /// Waits until a connection with Axon Server has been established or until the timeout has been reached.
    /// </summary>
    /// <returns>A <see cref="Task"/> that confirms a connection has been established or faulted with a <see cref="TimeoutException"/>.</returns>
    /// <exception cref="TaskCanceledException">Thrown if this instance is closed or disposed.</exception>
    Task WaitUntilConnectedAsync(TimeSpan timeout) => WaitUntilConnectedAsync().WaitAsync(timeout);
    
    /// <summary>
    /// Indicates if the connection is able to send and receive instructions.
    /// </summary>
    bool IsReady { get; }
    
    /// <summary>
    /// Waits until a connection is able to send and receive instructions.
    /// </summary>
    /// <returns>A <see cref="Task"/> that confirms a connection is able to send and receive instructions.</returns>
    /// <exception cref="TaskCanceledException">Thrown if this instance is closed or disposed.</exception>
    Task WaitUntilReadyAsync();
    
    /// <summary>
    /// Waits until a connection is able to send and receive instructions or until the timeout has been reached.
    /// </summary>
    /// <returns>A <see cref="Task"/> that confirms a connection is able to send and receive instructions or faulted with a <see cref="TimeoutException"/>.</returns>
    /// <exception cref="TaskCanceledException">Thrown if this instance is closed or disposed.</exception>
    Task WaitUntilReadyAsync(TimeSpan timeout) => WaitUntilReadyAsync().WaitAsync(timeout);
    
    /// <summary>
    /// Indicates if the connection with Axon Server is closed.
    /// </summary>
    bool IsClosed { get; }
    
    /// <summary>
    /// Requests to close the connection with Axon Server.
    /// </summary>
    /// <returns>A <see cref="Task"/> that confirms the connection has been closed.</returns>
    /// <remarks>This member is an alias for <see cref="IAsyncDisposable.DisposeAsync()"/>.</remarks>
    Task CloseAsync();
}