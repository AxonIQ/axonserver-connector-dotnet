namespace AxonIQ.AxonServer.Connector;

public class AxonServerConnectionFactory
{
    public AxonServerConnectionFactory(AxonServerConnectionFactoryOptions options)
    {
        if (options == null) 
            throw new ArgumentNullException(nameof(options));

        ComponentName = options.ComponentName;
    }

    public ComponentName ComponentName { get; }
}