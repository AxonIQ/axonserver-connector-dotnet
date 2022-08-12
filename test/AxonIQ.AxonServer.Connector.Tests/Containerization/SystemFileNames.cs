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

public class SystemFileNames
{
    /// <summary>
    /// File suffix for bloom files. Default value is .bloom.
    /// </summary>
    public string? EventBloomIndexSuffix { get; set; }

    /// <summary>
    /// File suffix for events files. Default value is .events.
    /// </summary>
    public string? EventEventsSuffix { get; set; }

    /// <summary>
    /// File suffix for index files. Default value is .index.
    /// </summary>
    public string? EventIndexSuffix { get; set; }

    /// <summary>
    /// File suffix for snapshot bloom files. Default value is .sbloom.
    /// </summary>
    public string? SnapshotBloomIndexSuffix { get; set; }

    /// <summary>
    /// File suffix for snapshot files. Default value is .snapshots.
    /// </summary>
    public string? SnapshotEventsSuffix { get; set; }

    /// <summary>
    /// File suffix for index files for snapshots. Default value is .sindex.
    /// </summary>
    public string? SnapshotIndexSuffix { get; set; }

    /// <summary>
    /// File suffix for index files for transaction logs. Default value is .index.
    /// </summary>
    public string? ReplicationIndexSuffix { get; set; }

    /// <summary>
    /// File suffix for transaction log files. Default value is .log.
    /// </summary>
    public string? ReplicationLogSuffix { get; set; }

    public string[] Serialize()
    {
        var properties = new List<string>();
        if (!string.IsNullOrEmpty(EventBloomIndexSuffix))
        {
            properties.Add($"axoniq.axonserver.event.bloom-index-suffix={EventBloomIndexSuffix}");
        }

        if (!string.IsNullOrEmpty(EventEventsSuffix))
        {
            properties.Add($"axoniq.axonserver.event.events-suffix={EventEventsSuffix}");
        }

        if (!string.IsNullOrEmpty(EventIndexSuffix))
        {
            properties.Add($"axoniq.axonserver.event.index-suffix={EventIndexSuffix}");
        }

        if (!string.IsNullOrEmpty(SnapshotBloomIndexSuffix))
        {
            properties.Add($"axoniq.axonserver.snapshot.bloom-index-suffix={SnapshotBloomIndexSuffix}");
        }

        if (!string.IsNullOrEmpty(SnapshotEventsSuffix))
        {
            properties.Add($"axoniq.axonserver.snapshot.events-suffix={SnapshotEventsSuffix}");
        }

        if (!string.IsNullOrEmpty(SnapshotIndexSuffix))
        {
            properties.Add($"axoniq.axonserver.snapshot.index-suffix={SnapshotIndexSuffix}");
        }

        if (!string.IsNullOrEmpty(ReplicationIndexSuffix))
        {
            properties.Add($"axoniq.axonserver.replication.index-suffix={ReplicationIndexSuffix}");
        }

        if (!string.IsNullOrEmpty(ReplicationLogSuffix))
        {
            properties.Add($"axoniq.axonserver.replication.log-suffix={ReplicationLogSuffix}");
        }

        return properties.ToArray();
    }

    public void CopyTo(SystemFileNames other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        other.EventBloomIndexSuffix = EventBloomIndexSuffix;
        other.EventEventsSuffix = EventEventsSuffix;
        other.EventIndexSuffix = EventIndexSuffix;
        other.SnapshotBloomIndexSuffix = SnapshotBloomIndexSuffix;
        other.SnapshotEventsSuffix = SnapshotEventsSuffix;
        other.SnapshotIndexSuffix = SnapshotIndexSuffix;
        other.ReplicationIndexSuffix = ReplicationIndexSuffix;
        other.ReplicationLogSuffix = ReplicationLogSuffix;
    }
}