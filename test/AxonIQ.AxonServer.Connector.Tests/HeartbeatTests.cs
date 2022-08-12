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

using AxonIQ.AxonServer.Connector.Tests.Containerization;
using Io.Axoniq.Axonserver.Grpc.Control;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
public class HeartbeatTests
{
    private readonly IAxonServer _container;

    public HeartbeatTests(AxonServerWithAccessControlDisabled container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }
    
    [Fact]
    public async Task HeartbeatGetsAcknowledged()
    {
        var channel = _container.CreateGrpcChannel(null);
        var context = Context.Default;
        var callInvoker = channel.Intercept(metadata =>
        {
            context.WriteTo(metadata);
            return metadata;
        });
        var service = new PlatformService.PlatformServiceClient(callInvoker);
        var stream = service.OpenStream();
        await stream.RequestStream.WriteAsync(new PlatformInboundInstruction
        {
            InstructionId = Guid.NewGuid().ToString("D"),
            Register = new ClientIdentification
            {
                ClientId = "1234",
                ComponentName = "789",
                Version = "1.0"
            }
        });
        var instructionId = Guid.NewGuid().ToString("D");
        await stream.RequestStream.WriteAsync(new PlatformInboundInstruction
        {
            InstructionId = instructionId,
            Heartbeat = new Heartbeat()
        });
        await foreach (var instruction in stream.ResponseStream.ReadAllAsync())
        {
            if (instruction.RequestCase == PlatformOutboundInstruction.RequestOneofCase.Ack
                && instruction.Ack.InstructionId == instructionId)
            {
                break;
            }
        }
    }
}

//Why? Because writes to the request stream need to be serialized