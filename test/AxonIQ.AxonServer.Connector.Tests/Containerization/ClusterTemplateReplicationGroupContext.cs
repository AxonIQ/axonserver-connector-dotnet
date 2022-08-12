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

using YamlDotNet.RepresentationModel;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class ClusterTemplateReplicationGroupContext
{
    public KeyValuePair<string, string>[]? Metadata { get; set; }
    public string? Name { get; set; }

    public YamlNode Serialize()
    {
        var node = new YamlMappingNode();
        if (!string.IsNullOrEmpty(Name))
        {
            node.Add("name", Name);    
        }

        if (Metadata != null && Metadata.Length != 0)
        {
            node.Add("metaData",
                new YamlMappingNode(Metadata.Select(metadatum =>
                    new KeyValuePair<YamlNode, YamlNode>(
                        new YamlScalarNode(metadatum.Key),
                        new YamlScalarNode(metadatum.Value))))
            );
        }
        else
        {
            node.Add("metaData", new YamlMappingNode());
        }

        return node;
    }
}