using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

internal static class QueryRequestExtensions
{
    private static bool SupportsServerStreaming(this QueryRequest request)
    {
        return request
            .ProcessingInstructions
            .SingleOrDefault(instruction => instruction.Key == ProcessingKey.ServerSupportsStreaming)
            ?.Value.BooleanValue ?? false;
    }
    
    private static bool SupportsClientStreaming(this QueryRequest request)
    {
        return request
            .ProcessingInstructions
            .SingleOrDefault(instruction => instruction.Key == ProcessingKey.ClientSupportsStreaming)
            ?.Value.BooleanValue ?? false;
    }
    
    public static bool SupportsStreaming(this QueryRequest request)
    {
        return request.SupportsClientStreaming() && request.SupportsServerStreaming();
    }
}