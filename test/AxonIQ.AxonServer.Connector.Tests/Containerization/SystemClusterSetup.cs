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

public class SystemClusterSetup
{
    /// <summary>
    /// For auto cluster option, set to the internal host name for the first node in the cluster. Default value is none.
    /// </summary>
    public string? AutoclusterFirst { get; set; }
    /// <summary>
    /// For auto cluster option, defines the list of contexts to connect to or create. Default value is none.
    /// </summary>
    public string[]? AutoclusterContexts { get; set; }
    /// <summary>
    /// Describes a cluster's configuration. Default value is ./cluster-template.yml.
    /// </summary>
    public string? ClusterTemplatePath { get; set; }

    public string[] Serialize()
    {
        var properties = new List<string>();
        if (!string.IsNullOrEmpty(AutoclusterFirst))
        {
            properties.Add($"axoniq.axonserver.autocluster.first={AutoclusterFirst}");
        }

        if (AutoclusterContexts != null && AutoclusterContexts.Length != 0)
        {
            properties.Add($"axoniq.axonserver.autocluster.contexts={string.Join(",", AutoclusterContexts)}");
        }

        if (!string.IsNullOrEmpty(ClusterTemplatePath))
        {
            properties.Add($"axoniq.axonserver.clustertemplate.path={ClusterTemplatePath}");
        }

        return properties.ToArray();
    }

    public void CopyTo(SystemClusterSetup other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        other.AutoclusterFirst = AutoclusterFirst;
        other.AutoclusterContexts = AutoclusterContexts?.ToArray();
        other.ClusterTemplatePath = ClusterTemplatePath;
    }
}