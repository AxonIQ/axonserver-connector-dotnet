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

public class SystemServerClusterMessaging
{
    /// <summary>
    /// Number of command messages that the master can initially send to a replica. Default value is 10000.
    /// </summary>
    public int? CommandFlowControlInitialNumberOfPermits { get; set; }
    /// <summary>
    /// Additional number of command messages that the master can send to replica. Default value is 5000.
    /// </summary>
    public int? CommandFlowControlNumberOfNewPermits { get; set; }
    /// <summary>
    /// When a replica reaches this threshold in remaining command messages, it sends a request with this additional number of command messages to receive. Default value is 5000.
    /// </summary>
    public int? CommandFlowControlNewPermitsThreshold { get; set; }
    /// <summary>
    /// Number of query messages that the master can initially send to a replica. Default value is 10000.
    /// </summary>
    public int? QueryFlowControlInitialNumberOfPermits { get; set; }
    /// <summary>
    /// Additional number of query messages that the master can send to replica. Default value is 5000.
    /// </summary>
    public int? QueryFlowControlNumberOfNewPermits { get; set; }
    /// <summary>
    /// When a replica reaches this threshold in remaining query messages, it sends a request with this additional number of query messages to receive. Default value is 5000.
    /// </summary>
    public int? QueryFlowControlNewPermitsThreshold { get; set; }
    
    public string[] Serialize()
    {
        var properties = new List<string>();

        if (CommandFlowControlInitialNumberOfPermits.HasValue)
        {
            properties.Add($"axoniq.axonserver.command-flow-control.initial-nr-of-permits={CommandFlowControlInitialNumberOfPermits.Value}");
        }

        if (CommandFlowControlNumberOfNewPermits.HasValue)
        {
            properties.Add($"axoniq.axonserver.command-flow-control.nr-of-new-permits={CommandFlowControlNumberOfNewPermits.Value}");
        }

        if (CommandFlowControlNewPermitsThreshold.HasValue)
        {
            properties.Add($"axoniq.axonserver.command-flow-control.new-permits-threshold={CommandFlowControlNewPermitsThreshold.Value}");
        }
        
        if (QueryFlowControlInitialNumberOfPermits.HasValue)
        {
            properties.Add($"axoniq.axonserver.query-flow-control.initial-nr-of-permits={QueryFlowControlInitialNumberOfPermits.Value}");
        }

        if (QueryFlowControlNumberOfNewPermits.HasValue)
        {
            properties.Add($"axoniq.axonserver.query-flow-control.nr-of-new-permits={QueryFlowControlNumberOfNewPermits.Value}");
        }

        if (QueryFlowControlNewPermitsThreshold.HasValue)
        {
            properties.Add($"axoniq.axonserver.query-flow-control.new-permits-threshold={QueryFlowControlNewPermitsThreshold.Value}");
        }

        return properties.ToArray();
    }

    public void CopyTo(SystemServerClusterMessaging other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        other.CommandFlowControlNewPermitsThreshold = CommandFlowControlNewPermitsThreshold;
        other.CommandFlowControlInitialNumberOfPermits = CommandFlowControlInitialNumberOfPermits;
        other.CommandFlowControlNumberOfNewPermits = CommandFlowControlNumberOfNewPermits;
        other.QueryFlowControlNewPermitsThreshold = QueryFlowControlNewPermitsThreshold;
        other.QueryFlowControlInitialNumberOfPermits = QueryFlowControlInitialNumberOfPermits;
        other.QueryFlowControlNumberOfNewPermits = QueryFlowControlNumberOfNewPermits;
    }
}