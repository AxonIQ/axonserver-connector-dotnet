using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

internal static class QueryReplyTranslation
{
    public static QueryReplyTranslator ForQuery(QueryProviderInbound inbound)
    {
        if (inbound == null) throw new ArgumentNullException(nameof(inbound));
        return reply =>
        {
            var instructionId1 = InstructionId.New();
            var instructionId2 = InstructionId.New();
            return reply switch
            {
                QueryReply.Send send => new[]
                {
                    new QueryProviderOutbound
                    {
                        InstructionId = string.IsNullOrEmpty(send.Response.MessageIdentifier) ? instructionId1.ToString() : send.Response.MessageIdentifier,
                        QueryResponse = new QueryResponse(send.Response)
                        {
                            MessageIdentifier =
                                string.IsNullOrEmpty(send.Response.MessageIdentifier) ? instructionId1.ToString() : send.Response.MessageIdentifier,
                            RequestIdentifier = inbound.Query.MessageIdentifier
                        }
                    }
                },
                QueryReply.Complete => new[]
                {
                    new QueryProviderOutbound
                    {
                        InstructionId = instructionId1.ToString(),
                        QueryComplete = new QueryComplete
                        {
                            MessageId = InstructionId.New().ToString(),
                            RequestId = inbound.Query.MessageIdentifier
                        }
                    }
                },
                QueryReply.CompleteWithError complete => new[]
                {
                    new QueryProviderOutbound
                    {
                        InstructionId = instructionId1.ToString(),
                        QueryResponse = new QueryResponse
                        {
                            ErrorMessage = complete.Error,
                            ErrorCode = complete.Error.ErrorCode,
                            MessageIdentifier = instructionId1.ToString(),
                            RequestIdentifier = inbound.Query.MessageIdentifier
                        }
                    },
                    new QueryProviderOutbound
                    {
                        InstructionId = instructionId2.ToString(),
                        QueryComplete = new QueryComplete
                        {
                            MessageId = instructionId2.ToString(),
                            RequestId = inbound.Query.MessageIdentifier
                        }
                    }
                },
                _ => Array.Empty<QueryProviderOutbound>()
            };
        };
    }
}