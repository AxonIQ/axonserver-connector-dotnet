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

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class SystemProperties
{
    public SystemNodeSetup NodeSetup { get; } = new();
    public SystemFileLocations FileLocations { get; } = new();
    public SystemFileNames FileNames { get; } = new();
    public SystemLogging Logging { get; } = new();
    public SystemClusterSetup ClusterSetup { get; } = new();
    public SystemAccessControl AccessControl { get; } = new();
    public SystemClientServerMessaging ClientServerMessaging { get; } = new();
    public SystemServerClusterMessaging ServerClusterMessaging { get; } = new();
    public SystemHttpPortSecurity HttpPortSecurity { get; } = new();
    public SystemGrpcPortSecurity GrpcPortSecurity { get; } = new();
    public SystemKeepAlive KeepAlive { get; } = new();
    //TODO: EventStore
    //TODO: Replication
    //TODO: MaintenanceTasks
    //TODO: Performance
    //TODO: Recovery
    //TODO: Plugins

    public SystemProperties Clone()
    {
        var clone = new SystemProperties();
        NodeSetup.CopyTo(clone.NodeSetup);
        FileLocations.CopyTo(clone.FileLocations);
        FileNames.CopyTo(clone.FileNames);
        Logging.CopyTo(clone.Logging);
        ClusterSetup.CopyTo(clone.ClusterSetup);
        AccessControl.CopyTo(clone.AccessControl);
        ClientServerMessaging.CopyTo(clone.ClientServerMessaging);
        ServerClusterMessaging.CopyTo(clone.ServerClusterMessaging);
        HttpPortSecurity.CopyTo(clone.HttpPortSecurity);
        GrpcPortSecurity.CopyTo(clone.GrpcPortSecurity);
        KeepAlive.CopyTo(clone.KeepAlive);
        return clone;
    }
    
    public string[] Serialize()
    {
        var properties = new List<string>();
        properties.AddRange(NodeSetup.Serialize());
        properties.AddRange(FileLocations.Serialize());
        properties.AddRange(FileNames.Serialize());
        properties.AddRange(Logging.Serialize());
        properties.AddRange(ClusterSetup.Serialize());
        properties.AddRange(AccessControl.Serialize());
        properties.AddRange(ClientServerMessaging.Serialize());
        properties.AddRange(ServerClusterMessaging.Serialize());
        properties.AddRange(HttpPortSecurity.Serialize());
        properties.AddRange(GrpcPortSecurity.Serialize());
        properties.AddRange(KeepAlive.Serialize());
        return properties.ToArray();
    }

    public Context[] ScanForContexts()
    {
        var contexts = new HashSet<Context>();
        if (ClusterSetup.AutoclusterContexts != null)
        {
            foreach (var name in ClusterSetup.AutoclusterContexts)
            {
                contexts.Add(new Context(name));
            }
        }

        return contexts.ToArray();
    }
}