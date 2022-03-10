// namespace AxonIQ.AxonServer.Connector;
//
// public class CompletionSource
// {
//     private readonly TaskCompletionSource _source;
//     private readonly List<Exception> _exceptions;
//
//     public CompletionSource()
//     {
//         _exceptions = new List<Exception>();
//         _source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
//     }
//
//     public void AddException(Exception exception)
//     {
//         _exceptions.Add(exception);
//     }
//
//     public void Complete()
//     {
//         if (_exceptions.Count > 0)
//         {
//             _source.TrySetException(_exceptions);
//         }
//         else
//         {
//             _source.TrySetResult();
//         }
//     }
//
//     public Task Completion => _source.Task;
// }
//
// public class Countdown
// {
//
//     public Countdown(int initialCount)
//     {
//         InitialCount = initialCount;
//         CurrentCount = initialCount;
//     }
//     
//     public int InitialCount { get; }
//     public int CurrentCount { get; private set; }
//
//     public bool Signal()
//     {
//         CurrentCount -= 1;
//         return CurrentCount == 0;
//     }
//
//     public void Reset()
//     {
//         CurrentCount = InitialCount;
//     }
// }