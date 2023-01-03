using System.Diagnostics;

namespace AxonIQ.AxonServer.Connector;

public static class Telemetry
{
    public static readonly string ServiceName = typeof(Telemetry).Namespace!;
    public static readonly string ServiceVersion = typeof(Telemetry).Assembly.GetName().Version!.ToString();

    public static readonly ActivitySource Source = new ActivitySource(ServiceName, ServiceVersion);
}