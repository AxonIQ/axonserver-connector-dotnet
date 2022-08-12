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

using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Connector.Tests.Containerization;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Command;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

Log.Logger = 
    new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
    .CreateLogger();

try
{
    var host =
        new HostBuilder()
            .UseSerilog()
            .Build();

    await using (var server = EmbeddedAxonServer.WithAccessControlDisabled(host.Services.GetRequiredService<ILogger<EmbeddedAxonServer>>()))
    {
        await server.InitializeAsync();

        var httpEndpoint = server.GetHttpEndpoint();
        Log.Information("Connect to {Host}",
            new UriBuilder { Host = httpEndpoint.Host, Port = httpEndpoint.Port }.Uri.AbsoluteUri);
        Console.ReadLine();

        var context = Context.Default;
        var component = new ComponentName("sample");
        var clientInstance1 = new ClientInstanceId("1");
        var clientInstance2 = new ClientInstanceId("2");

        var instance1 = new AxonServerConnectionFactory(AxonServerConnectionFactoryOptions
            .For(component, clientInstance1)
            .WithRoutingServers(server.GetGrpcEndpoint())
            .WithLoggerFactory(host.Services.GetRequiredService<ILoggerFactory>())
            .Build());
        var instance2 = new AxonServerConnectionFactory(AxonServerConnectionFactoryOptions
            .For(component, clientInstance2)
            .WithRoutingServers(server.GetGrpcEndpoint())
            .WithLoggerFactory(host.Services.GetRequiredService<ILoggerFactory>())
            .Build());

        var connection1 = await instance1.Connect(context);
        await connection1.WaitUntilConnected();

        var connection2 = await instance2.Connect(context);
        await connection2.WaitUntilConnected();

        var registration =
            await connection1.CommandChannel.RegisterCommandHandler(
                (command, ct) => Task.FromResult(new CommandResponse
                {
                    Payload = new SerializedObject
                    {
                        Type = "pong",
                        Revision = "1",
                        Data = ByteString.CopyFromUtf8("{}")
                    }
                }),
                new LoadFactor(100),
                new CommandName("ping"));

        await registration.WaitUntilCompleted();

        Log.Information("Command handler registration completed");
        Console.ReadLine();

        var request1 = new Command
        {
            Name = "ping"
        };
        var result1 = await connection2.CommandChannel.SendCommand(request1, CancellationToken.None);
        Log.Information(result1.ToString());
        
        var request2 = new Command
        {
            Name = "ping"
        };
        var result2 = await connection2.CommandChannel.SendCommand(request2, CancellationToken.None);
        Log.Information(result2.ToString());

        Log.Information("Got response from sending command");
        Console.ReadLine();
    }
}
catch (Exception exception)
{
    Log.Fatal(exception, "The host terminated unexpectedly because of an exception");
}
finally
{
    Log.CloseAndFlush();
}