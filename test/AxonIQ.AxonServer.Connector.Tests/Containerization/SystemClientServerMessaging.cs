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

public class SystemClientServerMessaging
{
    /// <summary>
    /// Maximum size of a message to be sent to the node (expressed as bytes). Default value is 4MB.
    /// </summary>
    public int? MaxMessageSize { get; set; }
    /// <summary>
    /// Number of messages that the server can initially send to a client. Default value is 1000.
    /// </summary>
    public int? InitialNumberOfPermits { get; set; }
    /// <summary>
    /// Additional number of messages that the server can send to a client. Default value is 500.
    /// </summary>
    public int? NumberOfNewPermits { get; set; }
    /// <summary>
    /// When a client reaches this threshold in remaining messages, it sends a request with additional number of messages to receive. Default value is 500.
    /// </summary>
    public int? NewPermitsThreshold { get; set; }
    
    public string[] Serialize()
    {
        var properties = new List<string>();
        if (MaxMessageSize.HasValue)
        {
            properties.Add($"axoniq.axonserver.max-message-size={MaxMessageSize.Value}");
        }

        if (InitialNumberOfPermits.HasValue)
        {
            properties.Add($"axoniq.axonserver.initial-nr-of-permits={InitialNumberOfPermits.Value}");
        }

        if (NumberOfNewPermits.HasValue)
        {
            properties.Add($"axoniq.axonserver.nr-of-new-permits={NumberOfNewPermits.Value}");
        }

        if (NewPermitsThreshold.HasValue)
        {
            properties.Add($"axoniq.axonserver.new-permits-threshold={NewPermitsThreshold.Value}");
        }

        return properties.ToArray();
    }

    public void CopyTo(SystemClientServerMessaging other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        other.MaxMessageSize = MaxMessageSize;
        other.NewPermitsThreshold = NewPermitsThreshold;
        other.InitialNumberOfPermits = InitialNumberOfPermits;
        other.NumberOfNewPermits = NumberOfNewPermits;
    }
}