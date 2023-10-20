using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

internal static class QueryReplyTranslation
{
    public static QueryReplyTranslator ForQuery(QueryRequest query)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));
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
                            RequestIdentifier = query.MessageIdentifier
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
                            RequestId = query.MessageIdentifier
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
                            RequestIdentifier = query.MessageIdentifier
                        }
                    },
                    new QueryProviderOutbound
                    {
                        InstructionId = instructionId2.ToString(),
                        QueryComplete = new QueryComplete
                        {
                            MessageId = instructionId2.ToString(),
                            RequestId = query.MessageIdentifier
                        }
                    }
                },
                _ => Array.Empty<QueryProviderOutbound>()
            };
        };
    }
    
    public static QueryReplyTranslator ForSubscriptionQuery(SubscriptionQuery query)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));
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
                        SubscriptionQueryResponse = new SubscriptionQueryResponse
                        {
                            MessageIdentifier =
                                string.IsNullOrEmpty(send.Response.MessageIdentifier) ? instructionId1.ToString() : send.Response.MessageIdentifier,
                            SubscriptionIdentifier = query.SubscriptionIdentifier,
                            
                            InitialResult = send.Response
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
                            RequestId = query.QueryRequest.MessageIdentifier
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
                            RequestIdentifier = query.QueryRequest.MessageIdentifier
                        }
                    },
                    new QueryProviderOutbound
                    {
                        InstructionId = instructionId2.ToString(),
                        QueryComplete = new QueryComplete
                        {
                            MessageId = instructionId2.ToString(),
                            RequestId = query.QueryRequest.MessageIdentifier
                        }
                    }
                },
                _ => Array.Empty<QueryProviderOutbound>()
            };
        };
    }
}