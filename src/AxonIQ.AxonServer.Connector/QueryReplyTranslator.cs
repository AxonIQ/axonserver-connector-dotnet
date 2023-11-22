using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

internal delegate IReadOnlyCollection<QueryProviderOutbound> QueryReplyTranslator(QueryReply reply);