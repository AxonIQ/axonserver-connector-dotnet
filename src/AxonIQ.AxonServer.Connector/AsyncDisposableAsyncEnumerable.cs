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

internal class AsyncDisposableAsyncEnumerable<T> : IAsyncEnumerable<T>
{
    private readonly IAsyncEnumerable<T> _enumerable;
    private readonly IAsyncDisposable _disposable;

    public AsyncDisposableAsyncEnumerable(IAsyncEnumerable<T> enumerable, IAsyncDisposable disposable)
    {
        _enumerable = enumerable;
        _disposable = disposable;
    }
    
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new())
    {
        return new AsyncDisposableAsyncEnumerator(_enumerable.GetAsyncEnumerator(cancellationToken), _disposable);
    }

    private class AsyncDisposableAsyncEnumerator : IAsyncEnumerator<T>
    {
        private readonly IAsyncEnumerator<T> _enumerator;
        private readonly IAsyncDisposable _disposable;

        public AsyncDisposableAsyncEnumerator(IAsyncEnumerator<T> enumerator, IAsyncDisposable disposable)
        {
            _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
            _disposable = disposable ?? throw new ArgumentNullException(nameof(disposable));
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return _enumerator.MoveNextAsync();
        }

        public T Current => _enumerator.Current;

        public async ValueTask DisposeAsync()
        {
            await _enumerator.DisposeAsync();
            await _disposable.DisposeAsync();
        }
    }
}