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

using Grpc.Core;

namespace AxonIQ.AxonServer.Connector;

public class AsyncDuplexStreamingCallProxy<TRequest, TResponse>
{
    private readonly Func<AsyncDuplexStreamingCall<TRequest, TResponse>?> _factory;

    public AsyncDuplexStreamingCallProxy(Func<AsyncDuplexStreamingCall<TRequest, TResponse>?> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public AsyncDuplexStreamingCall<TRequest, TResponse> Call => _factory() ?? throw new NotSupportedException();
    
    public IAsyncStreamReader<TResponse> ResponseStream => Call.ResponseStream;

    public IClientStreamWriter<TRequest> RequestStream => Call.RequestStream;

    public Task<Metadata> ResponseHeadersAsync => Call.ResponseHeadersAsync;

    public Status GetStatus() => Call.GetStatus();

    public Metadata GetTrailers() => Call.GetTrailers();
}