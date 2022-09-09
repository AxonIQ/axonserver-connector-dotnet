using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

internal static class QueryRequestExtensions
{
    private static bool SuppportsServerStreaming(this QueryRequest request)
    {
        return request
            .ProcessingInstructions
            .SingleOrDefault(instruction => instruction.Key == ProcessingKey.ServerSupportsStreaming)
            ?.Value.BooleanValue ?? false;
    }
    
    private static bool SuppportsClientStreaming(this QueryRequest request)
    {
        return request
            .ProcessingInstructions
            .SingleOrDefault(instruction => instruction.Key == ProcessingKey.ClientSupportsStreaming)
            ?.Value.BooleanValue ?? false;
    }
    
    public static bool SuppportsStreaming(this QueryRequest request)
    {
        return request.SuppportsClientStreaming() && request.SuppportsServerStreaming();
    }
}