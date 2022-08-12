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

namespace AxonIQ.AxonServer.Connector;

public class CountdownCompletionSource
{
    private readonly TaskCompletionSource _source;
    private readonly List<Exception> _exceptions;
    private readonly Task _completion;

    public CountdownCompletionSource(int initialCount)
    {
        if (initialCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(initialCount), initialCount,
                "The initial count can not be less than or equal to 0");

        InitialCount = initialCount;
        CurrentCount = 0;
        _exceptions = new List<Exception>();
        _source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _completion = InitialCount != 0 ? _source.Task : Task.CompletedTask;
    }

    public int InitialCount { get; }
    public int CurrentCount { get; private set; }

    public Task Completion => _completion;

    public bool SignalFault(Exception exception)
    {
        if (_source.Task.IsCompleted) return false;

        _exceptions.Add(exception);
        CurrentCount++;

        return CurrentCount == InitialCount && _source.TrySetException(_exceptions);
    }

    public bool SignalSuccess()
    {
        if (_source.Task.IsCompleted) return false;

        CurrentCount++;

        return CurrentCount == InitialCount &&
               (_exceptions.Count > 0
                   ? _source.TrySetException(_exceptions)
                   : _source.TrySetResult());
    }

    public bool Fault(Exception exception)
    {
        if (_source.Task.IsCompleted) return false;
        
        return _source.TrySetException(exception);
    }
}