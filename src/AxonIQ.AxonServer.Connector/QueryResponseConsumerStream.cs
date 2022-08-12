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

using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

internal class QueryResponseConsumerStream : IAsyncEnumerable<QueryResponse>
{
    private readonly IAsyncEnumerable<QueryResponse> _enumerable;
    private readonly IDisposable _disposable;

    public QueryResponseConsumerStream(IAsyncEnumerable<QueryResponse> enumerable, IDisposable disposable)
    {
        _enumerable = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
        _disposable = disposable ?? throw new ArgumentNullException(nameof(disposable));
    }
    
    public IAsyncEnumerator<QueryResponse> GetAsyncEnumerator(CancellationToken cancellationToken = new())
    {
        return new DisposableAsyncEnumerator(_enumerable.GetAsyncEnumerator(cancellationToken), _disposable);
    }

    private class DisposableAsyncEnumerator : IAsyncEnumerator<QueryResponse>
    {
        private readonly IAsyncEnumerator<QueryResponse> _enumerator;
        private readonly IDisposable _disposable;
        
        public DisposableAsyncEnumerator(IAsyncEnumerator<QueryResponse> enumerator, IDisposable disposable)
        {
            _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
            _disposable = disposable ?? throw new ArgumentNullException(nameof(disposable));
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return _enumerator.MoveNextAsync();
        }

        public QueryResponse Current => _enumerator.Current;

        public async ValueTask DisposeAsync()
        {
            await _enumerator.DisposeAsync();
            _disposable.Dispose();
        }
    }
}