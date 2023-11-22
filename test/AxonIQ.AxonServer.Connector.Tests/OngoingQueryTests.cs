// namespace AxonIQ.AxonServer.Connector.Tests;
//
// public class OngoingQueryTests
// {
//     
// }
//
// internal interface IQueryExecution : IAsyncDisposable
// {
//     InstructionId QueryId { get; }
//     
//     void Request(long count);
//
//     void Cancel();
// }
//
// internal class QueryExecution : IQueryExecution
// {
//     public QueryExecution(InstructionId id, IFlowControl? flowControl)
//     {
//         QueryId = id;
//         FlowControl = flowControl;
//     }
//     
//     public InstructionId QueryId { get; }
//
//     public IFlowControl? FlowControl { get; }
//     
//     public void Request(long count)
//     {
//         FlowControl?.Request(count);
//     }
//
//     public void Cancel()
//     {
//         throw new NotImplementedException();
//     }
//     
//     
//     public ValueTask DisposeAsync()
//     {
//         throw new NotImplementedException();
//     }
// }
//
// // QueryCancel message causes the related query execution to be disposed
// // Disposing the query execution disposes the forwarder
//
// internal static class ExecuteQuery
// {
//     public static IQueryBuilder WithOneHandler(Task handler)
//     {
//         
//     }
//
//     public static IQueryBuilder WithManyHandlers(Task[] handlers)
//     {
//         
//     }
// }
//
// internal  class HandledByOneQueryBuilder : IQueryBuilder
// {
//     private readonly Task _handler;
//     private IFlowControl? _control;
//
//     public HandledByOneQueryBuilder(Task handler)
//     {
//         _handler = handler;
//     }
//
//     public IQueryBuilder WithFlowControl(IFlowControl control)
//     {
//         _control = control ?? throw new ArgumentNullException(nameof(control));
//         return this;
//     }
//
//     public IQueryBuilder WithCancellationTokenSource(CancellationTokenSource source)
//     {
//         _source = new 
//     }
// }
// internal interface IQueryBuilder
// {
//     IQueryBuilder WithFlowControl(IFlowControl control);
// }