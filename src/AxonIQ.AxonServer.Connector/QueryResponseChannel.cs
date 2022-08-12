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

public class QueryResponseChannel : IQueryResponseChannel
{
    private readonly QueryRequest _request;
    private readonly WriteQueryProviderOutbound _writer;

    public QueryResponseChannel(QueryRequest request, WriteQueryProviderOutbound writer)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }
        
    public ValueTask WriteAsync(QueryResponse response)
    {
        return _writer(new QueryProviderOutbound
        {
            QueryResponse = response,
            InstructionId = InstructionId.New().ToString()
        });
    }

    public ValueTask CompleteAsync()
    {
        var instructionId = InstructionId.New().ToString();
        return _writer(new QueryProviderOutbound
        {
            QueryComplete = new QueryComplete
            {
                RequestId = _request.MessageIdentifier,
                MessageId = instructionId
            },
            InstructionId = instructionId
        });
    }

    public async ValueTask CompleteWithErrorAsync(ErrorMessage errorMessage)
    {
        var instructionId1 = InstructionId.New().ToString();
        await _writer(new QueryProviderOutbound
        {
            QueryResponse = new QueryResponse
            {
                ErrorMessage = errorMessage,
                MessageIdentifier = instructionId1
            },
            InstructionId = instructionId1
        });
        
        var instructionId2 = InstructionId.New().ToString();
        await _writer(new QueryProviderOutbound
        {
            QueryComplete = new QueryComplete
            {
                RequestId = _request.MessageIdentifier,
                MessageId = instructionId2
            },
            InstructionId = instructionId2
        });
    }

    public async ValueTask CompleteWithErrorAsync(ErrorCategory errorCategory, ErrorMessage errorMessage)
    {
        var instructionId1 = InstructionId.New().ToString();
        await _writer(new QueryProviderOutbound
        {
            QueryResponse = new QueryResponse
            {
                ErrorMessage = errorMessage,
                ErrorCode = errorCategory.ToString(),
                MessageIdentifier = instructionId1
            },
            InstructionId = instructionId1
        });
        
        var instructionId2 = InstructionId.New().ToString();
        await _writer(new QueryProviderOutbound
        {
            QueryComplete = new QueryComplete
            {
                RequestId = _request.MessageIdentifier,
                MessageId = instructionId2
            },
            InstructionId = instructionId2
        });
    }
}