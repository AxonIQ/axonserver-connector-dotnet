using System.Diagnostics;

namespace AxonIQ.AxonServer.Connector;

internal static class Telemetry
{
    public static readonly string ServiceName = typeof(Telemetry).Namespace!;
    public static readonly string ServiceVersion = typeof(Telemetry).Assembly.GetName().Version!.ToString();

    public static readonly ActivitySource Source = new (ServiceName, ServiceVersion);

    private const string AxonPrefix = "axoniq.";
    private const string ClientIdTag = AxonPrefix + "client_id";
    private const string ComponentNameTag = AxonPrefix + "component_name";
    private const string ContextTag = AxonPrefix + "context";
    private const string MessageIdTag = AxonPrefix + "message_id";
    private const string CommandNameTag = AxonPrefix + "command_name";

    public static Activity? SetClientIdTag(this Activity? activity, ClientInstanceId value)
    {
        return activity?.SetTag(ClientIdTag, value.ToString());
    }
    
    public static Activity? SetComponentNameTag(this Activity? activity, ComponentName value)
    {
        return activity?.SetTag(ComponentNameTag, value.ToString());
    }
    
    public static Activity? SetContextTag(this Activity? activity, Context value)
    {
        return activity?.SetTag(ContextTag, value.ToString());
    }
    
    public static Activity? SetMessageIdTag(this Activity? activity, string value)
    {
        return activity?.SetTag(MessageIdTag, value);
    }
    
    public static Activity? SetCommandNameTag(this Activity? activity, string value)
    {
        return activity?.SetTag(CommandNameTag, value);
    }
}