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

public class CallInvokerProxy : CallInvoker
{
    private readonly Func<CallInvoker?> _factory;
    private readonly CallInvoker _serviceNotAvailableCallInvoker;

    public CallInvokerProxy(Func<CallInvoker?> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _serviceNotAvailableCallInvoker = new FaultyCallInvoker(
            new Status(StatusCode.Unavailable, nameof(StatusCode.Unavailable)),
            Metadata.Empty,
            "The axon server is not ready to handle the request");
    }

    private CallInvoker CallInvoker => _factory() ?? _serviceNotAvailableCallInvoker;

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
    {
        return CallInvoker.BlockingUnaryCall(method, host, options, request);
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
    {
        return CallInvoker.AsyncUnaryCall(method, host, options, request);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options,
        TRequest request)
    {
        return CallInvoker.AsyncServerStreamingCall(method, host, options, request);
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
    {
        return CallInvoker.AsyncClientStreamingCall(method, host, options);
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
    {
        return CallInvoker.AsyncDuplexStreamingCall(method, host, options);
    }
}