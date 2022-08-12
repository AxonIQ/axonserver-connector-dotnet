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

using System.Net;
using AutoFixture;

namespace AxonIQ.AxonServer.Connector.Tests;

public static class FixtureExtensions
{
    public static void CustomizeComponentName(this IFixture fixture)
    {
        fixture.Customize<ComponentName>(composer =>
            composer
                .FromFactory(ComponentName.GenerateRandomName)
                .OmitAutoProperties());
    }

    public static void CustomizeContext(this IFixture fixture)
    {
        fixture.Customize<Context>(composer =>
            composer
                .FromFactory((string value) => new Context(value))
                .OmitAutoProperties());
    }

    public static void CustomizeClientInstanceId(this IFixture fixture)
    {
        fixture.Customize<ClientInstanceId>(composer =>
            composer
                .FromFactory((ComponentName name) => ClientInstanceId.GenerateFrom(name))
                .OmitAutoProperties());
    }
    
    public static void CustomizeCommandHandlerId(this IFixture fixture)
    {
        fixture.Customize<CommandHandlerId>(composer =>
            composer
                .FromFactory(CommandHandlerId.New)
                .OmitAutoProperties());
    }
    
    public static void CustomizeSubscriptionId(this IFixture fixture)
    {
        fixture.Customize<SubscriptionId>(composer =>
            composer
                .FromFactory(SubscriptionId.New)
                .OmitAutoProperties());
    }
    
    public static void CustomizeLoadFactor(this IFixture fixture)
    {
        fixture.Customize<LoadFactor>(composer =>
            composer
                .FromFactory((int value) => new LoadFactor(Math.Abs(value)))
                .OmitAutoProperties());
    }
    
    public static void CustomizePermitCount(this IFixture fixture)
    {
        fixture.Customize<PermitCount>(composer =>
            composer
                .FromFactory((long value) => new PermitCount(value == 0L ? 1 : Math.Abs(value)))
                .OmitAutoProperties());
    }
    
    public static void CustomizePermitCounter(this IFixture fixture)
    {
        fixture.Customize<PermitCounter>(composer =>
            composer
                .FromFactory((long value) => new PermitCounter(Math.Abs(value)))
                .OmitAutoProperties());
    }

    public static void CustomizeLocalHostDnsEndPointInReservedPortRange(this IFixture fixture)
    {
        // REMARK: Due to the randomization of data we might accidentally pick a port on which an AxonServer is listening.
        // By s no Axon Server will be listening on a host and port in the reserved port range [0..1024], we prevent this
        // accident from happening.
        fixture.Customize<DnsEndPoint>(composer =>
            composer
                .FromFactory((int port) => new DnsEndPoint("127.0.0.0", port % 1024))
                .OmitAutoProperties());
    }
}