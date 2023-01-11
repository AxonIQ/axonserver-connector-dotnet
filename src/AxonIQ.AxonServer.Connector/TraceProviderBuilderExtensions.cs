using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AxonIQ.AxonServer.Connector;

public static class TraceProviderBuilderExtensions
{
    public static TracerProviderBuilder AddAxonServerConnectorInstrumentation(this TracerProviderBuilder builder)
    {
        builder
            .AddSource(Telemetry.ServiceName)
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(Telemetry.ServiceName, serviceVersion: Telemetry.ServiceVersion));
        return builder;
    }
}