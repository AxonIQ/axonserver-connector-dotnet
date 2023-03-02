using Grpc.Net.Client;

namespace AxonIQ.AxonServer.Connector;

internal static class GrpcChannelOptionsExtensions
{
    public static GrpcChannelOptions Clone(this GrpcChannelOptions options) =>
        new()
        {
            CompressionProviders = options.CompressionProviders,
            Credentials = options.Credentials,
            DisableResolverServiceConfig = options.DisableResolverServiceConfig,
            DisposeHttpClient = options.DisposeHttpClient,
            HttpClient = options.HttpClient,
            HttpHandler = options.HttpHandler,
            InitialReconnectBackoff = options.InitialReconnectBackoff,
            LoggerFactory = options.LoggerFactory,
            MaxReceiveMessageSize = options.MaxReceiveMessageSize,
            MaxReconnectBackoff = options.MaxReconnectBackoff,
            MaxRetryAttempts = options.MaxRetryAttempts,
            MaxRetryBufferPerCallSize = options.MaxRetryBufferSize,
            MaxRetryBufferSize = options.MaxRetryBufferSize,
            MaxSendMessageSize = options.MaxSendMessageSize,
            ServiceConfig = options.ServiceConfig,
            ServiceProvider = options.ServiceProvider,
            ThrowOperationCanceledOnCancellation = options.ThrowOperationCanceledOnCancellation,
            UnsafeUseInsecureChannelCallCredentials = options.UnsafeUseInsecureChannelCallCredentials
        };

    public static GrpcChannelOptions ConfigureAxonOptions(this GrpcChannelOptions options)
    {
        options.ThrowOperationCanceledOnCancellation = true;
        return options;
    }
}