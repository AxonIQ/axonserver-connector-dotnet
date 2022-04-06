// using AxonIQ.AxonServer.Grpc.Query;
// using Grpc.Core;
//
// namespace AxonIQ.AxonServer.Connector;
//
// public class QueryChannel
// {
//     private readonly CallInvoker _callInvoker;
//
//     public QueryChannel(CallInvoker callInvoker)
//     {
//         _callInvoker = callInvoker;
//         Service = new QueryService.QueryServiceClient(callInvoker);
//     }
//
//     public QueryService.QueryServiceClient Service { get; }
// }