/*
 * Copyright (c) 2022. AxonIQ
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

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