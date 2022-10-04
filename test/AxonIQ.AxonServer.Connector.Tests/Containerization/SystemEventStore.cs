using System.Globalization;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class SystemEventStore
{
    /// <summary>
    /// False-positive percentage allowed for bloom index. Decreasing the value increases the size of the bloom indexes. Default value is 0.03. 
    /// </summary>
    public double? EventBloomIndexFpp { get; set; }

    /// <summary>
    /// Interval to force syncing files to disk (ms). Default value is 1000.
    /// </summary>
    public int? EventForceInterval { get; set; }
    
    /// <summary>
    /// Delay to clear ByteBuffers from off-heap memory for writable segments. Default value is 15.
    /// </summary>
    public int? EventPrimaryCleanupDelay { get; set; }

    /// <summary>
    /// Delay to clear ByteBuffers from off-heap memory for read-only segments. Default value is 15. 
    /// </summary>
    public int? EventSecondaryCleanupDelay { get; set; }
    
    /// <summary>
    /// Size for new storage segments. Default value is 256Mb.
    /// </summary>
    public string? EventSegmentSize { get; set; }
    
    /// <summary>
    /// Interval (ms) to check if there are files that are complete and can be closed. Default value is 1000. 
    /// </summary>
    public int? EventSyncInterval { get; set; }
    
    /// <summary>
    /// Number of segments to validate to on startup after unclean shutdown. Default value is 10. 
    /// </summary>
    public int? EventValidationSegments { get; set; }
    /// <summary>
    /// Number of recent segments that Axon Server keeps memory mapped. Default value is 5. 
    /// </summary>
    public int? EventMemoryMappedSegments { get; set; }
    /// <summary>
    /// Number of events to prefetch from disk when streaming events to the client. Default value is 50. 
    /// </summary>
    public int? EventsPerSegmentPrefetch { get; set; }
    /// <summary>
    /// False-positive percentage allowed for bloom index for snapshots. Decreasing the value increases the size of the bloom indexes. Default value is 0.03. 
    /// </summary>
    public double? SnapshotBloomIndexFpp { get; set; }
    /// <summary>
    /// Interval to force syncing files to disk (ms). Default value is 1000.
    /// </summary>
    public int? SnapshotForceInterval { get; set; }
    
    /// <summary>
    /// Delay to clear ByteBuffers from off-heap memory for writable segments. Default value is 15.
    /// </summary>
    public int? SnapshotPrimaryCleanupDelay { get; set; }

    /// <summary>
    /// Delay to clear ByteBuffers from off-heap memory for read-only segments. Default value is 15. 
    /// </summary>
    public int? SnapshotSecondaryCleanupDelay { get; set; }
    
    /// <summary>
    /// Size for new storage segments. Default value is 256Mb.
    /// </summary>
    public string SnapshotSegmentSize { get; set; }
    
    /// <summary>
    /// Interval (ms) to check if there are files that are complete and can be closed. Default value is 1000. 
    /// </summary>
    public int? SnapshotSyncInterval { get; set; }
    
    /// <summary>
    /// Number of segments to validate to on startup after unclean shutdown. Default value is 10. 
    /// </summary>
    public int? SnapshotValidationSegments { get; set; }
    /// <summary>
    /// Number of recent segments that Axon Server keeps memory mapped. Default value is 5. 
    /// </summary>
    public int? SnapshotMemoryMappedSegments { get; set; }
    /// <summary>
    /// Default value is 200.
    /// </summary>
    public int? QueryLimit { get; set; }
    
    /// <summary>
    /// Timeout for event trackers while waiting for new permits. Default value is 120000.
    /// </summary>
    public int? NewPermitsTimeout { get; set; }
    /// <summary>
    /// Force sending an event on an event tracker after this number of blacklisted events. Ensures that the token in the client application is updated. Default value is 1000. 
    /// </summary>
    public int? BlacklistedSendAfter { get; set; }
    /// <summary>
    /// Maximum number of events that can be sent in a single transaction. Default value is 32767. 
    /// </summary>
    public int? MaxEventsPerTransaction { get; set; }
    /// <summary>
    /// (Since 4.4.7, EE only) Sets the default index type to be used for new contexts. Values are JUMP_SKIP_INDEX and BLOOM_FILTER_INDEX. Default value is JUMP_SKIP_INDEX. 
    /// </summary>
    public string? EnterpriseDefaultIndexType { get; set; }
    /// <summary>
    /// (Since 4.4.14) Sets how to handle validation errors while reading aggregates from the event store. Values are LOG and FAIL. Default value is LOG. 
    /// </summary>
    public string? ReadSequenceValidationStrategy { get; set; }
    /// <summary>
    /// By default, AxonServer will determine whether to use memory mapped indexes for event files in the event store based on operating system and java version, in rare cases it may be useful to override the default. 
    /// </summary>
    public bool? EventUseMMapIndex { get; set; }
    /// <summary>
    /// Option to forcefully close unused memory mapped files instead of leaving the garbage collector do this, by default, AxonServer will determine this based on operating system and java version, in rare cases it may be useful to override the default 
    /// </summary>
    public bool? EventForceCleanMMapIndex { get; set; }
    /// <summary>
    /// Ensures that backpressure signals from clients are split into batches. The initial request amount is {prefetch}*5, and subsequent (or replenishing) request amount is {prefetch}. Default value is 5. 
    /// </summary>
    public int? EventAggregatePrefetch { get; set; } 
    /// <summary>
    /// Number of retries for reading event aggregate stream. Default value is 3. 
    /// </summary>
    public int? EventAggegrateRetryAttempts { get; set; }
    /// <summary>
    /// Delay (ms) between retries for reading event aggregate stream. Default value is 100. 
    /// </summary>
    public int? EventAggregateRetryDelay { get; set; }
    /// <summary>
    /// Number of retries for finding an event store. Default value is 3. 
    /// </summary>
    public int? EventLeaderRetryAttempts { get; set; }
    /// <summary>
    /// Delay (ms) between retries for finding an event store. Default value is 100. 
    /// </summary>
    public int? EventLeaderRetryDelay { get; set; }
    /// <summary>
    /// Delay (ms) between checking if tracking event processors are waiting for new permits for a long time. Default value is 2000. 
    /// </summary>
    public int? EventProcessorPermitsCheck { get; set; }
    /// <summary>
    /// Whether to check for invalid sequence numbers on appending a snapshot. Default value is true.
    /// </summary>
    public bool? CheckSequenceNrForSnapshots { get; set; }

    public string[] Serialize()
    {
        var properties = new List<string>();
        if (EventBloomIndexFpp.HasValue)
        {
            properties.Add($"axoniq.axonserver.event.bloom-index-fpp={EventBloomIndexFpp.Value.ToString(CultureInfo.InvariantCulture)}");
        }
        if (EventForceInterval.HasValue)
        {
            properties.Add($"axoniq.axonserver.event.force-interval={EventForceInterval.Value}");
        }
        if (EventPrimaryCleanupDelay.HasValue)
        {
            properties.Add($"axoniq.axonserver.event.primary-cleanup-delay={EventPrimaryCleanupDelay.Value}");
        }
        if (EventSecondaryCleanupDelay.HasValue)
        {
            properties.Add($"axoniq.axonserver.event.secondary-cleanup-delay={EventSecondaryCleanupDelay.Value}");
        }
        if (!string.IsNullOrEmpty(EventSegmentSize))
        {
            properties.Add($"axoniq.axonserver.event.segment-size={EventSegmentSize}");
        }
        if (EventSyncInterval.HasValue)
        {
            properties.Add($"axoniq.axonserver.event.sync-interval={EventSyncInterval.Value}");
        }
        if (EventValidationSegments.HasValue)
        {
            properties.Add($"axoniq.axonserver.event.validation-segments={EventValidationSegments.Value}");
        }
        if (EventMemoryMappedSegments.HasValue)
        {
            properties.Add($"axoniq.axonserver.event.memory-mapped-segments={EventMemoryMappedSegments.Value}");
        }
        if (EventsPerSegmentPrefetch.HasValue)
        {
            properties.Add($"axoniq.axonserver.event.events-per-segment-prefetch={EventsPerSegmentPrefetch.Value}");
        }
        if (SnapshotBloomIndexFpp.HasValue)
        {
            properties.Add($"axoniq.axonserver.snapshot.bloom-index-fpp={SnapshotBloomIndexFpp.Value.ToString(CultureInfo.InvariantCulture)}");
        }
        if (SnapshotForceInterval.HasValue)
        {
            properties.Add($"axoniq.axonserver.snapshot.force-interval={SnapshotForceInterval.Value}");
        }
        if (SnapshotPrimaryCleanupDelay.HasValue)
        {
            properties.Add($"axoniq.axonserver.snapshot.primary-cleanup-delay={SnapshotPrimaryCleanupDelay.Value}");
        }
        if (SnapshotSecondaryCleanupDelay.HasValue)
        {
            properties.Add($"axoniq.axonserver.snapshot.secondary-cleanup-delay={SnapshotSecondaryCleanupDelay.Value}");
        }
        if (!string.IsNullOrEmpty(SnapshotSegmentSize))
        {
            properties.Add($"axoniq.axonserver.snapshot.segment-size={SnapshotSegmentSize}");
        }
        if (SnapshotSyncInterval.HasValue)
        {
            properties.Add($"axoniq.axonserver.snapshot.sync-interval={SnapshotSyncInterval.Value}");
        }
        if (SnapshotValidationSegments.HasValue)
        {
            properties.Add($"axoniq.axonserver.snapshot.validation-segments={SnapshotValidationSegments.Value}");
        }
        if (SnapshotMemoryMappedSegments.HasValue)
        {
            properties.Add($"axoniq.axonserver.snapshot.memory-mapped-segments={SnapshotMemoryMappedSegments.Value}");
        }
        if (QueryLimit.HasValue)
        {
            properties.Add($"axoniq.axonserver.query.limit={QueryLimit.Value}");
        }
        if (NewPermitsTimeout.HasValue)
        {
            properties.Add($"axoniq.axonserver.new-permits-timeout={NewPermitsTimeout.Value}");
        }
        if (BlacklistedSendAfter.HasValue)
        {
            properties.Add($"axoniq.axonserver.blacklisted-send-after={BlacklistedSendAfter.Value}");
        }
        if (MaxEventsPerTransaction.HasValue)
        {
            properties.Add($"axoniq.axonserver.max-events-per-transaction={MaxEventsPerTransaction.Value}");
        }
        if (!string.IsNullOrEmpty(EnterpriseDefaultIndexType))
        {
            properties.Add($"axoniq.axonserver.enterprise.default-index-type={EnterpriseDefaultIndexType}");
        }
        if (!string.IsNullOrEmpty(ReadSequenceValidationStrategy))
        {
            properties.Add($"axoniq.axonserver.read-sequence-validation-strategy={ReadSequenceValidationStrategy}");
        }
        if (EventUseMMapIndex.HasValue)
        {
            properties.Add($"axoniq.axonserver.event.use-mmap-index={EventUseMMapIndex.Value.ToString().ToLowerInvariant()}");
        }
        if (EventForceCleanMMapIndex.HasValue)
        {
            properties.Add($"axoniq.axonserver.event.force-clean-mmap-index={EventForceCleanMMapIndex.Value.ToString().ToLowerInvariant()}");
        }
        if (EventAggregatePrefetch.HasValue)
        {
            properties.Add($"axoniq.axonserver.event.aggregate.prefetch={EventAggregatePrefetch.Value}");
        }
        if (EventAggegrateRetryAttempts.HasValue)
        {
            properties.Add($"axoniq.axonserver.event.aggregate.retry.attempts={EventAggegrateRetryAttempts.Value}");
        }
        if (EventAggregateRetryDelay.HasValue)
        {
            properties.Add($"axoniq.axonserver.event.aggregate.retry.delay={EventAggregateRetryDelay.Value}");
        }
        if (EventLeaderRetryAttempts.HasValue)
        {
            properties.Add($"axoniq.axonserver.event.leader.retry.attempts={EventLeaderRetryAttempts.Value}");
        }
        if (EventLeaderRetryDelay.HasValue)
        {
            properties.Add($"axoniq.axonserver.event.leader.retry.delay={EventLeaderRetryDelay.Value}");
        }
        if (EventProcessorPermitsCheck.HasValue)
        {
            properties.Add($"axoniq.axonserver.event-processor-permits-check={EventProcessorPermitsCheck.Value}");
        }
        if (CheckSequenceNrForSnapshots.HasValue)
        {
            properties.Add($"axoniq.axonserver.check-sequence-nr-for-snapshots={CheckSequenceNrForSnapshots.Value.ToString().ToLowerInvariant()}");
        }
        return properties.ToArray();
    }
    
    public void CopyTo(SystemEventStore other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        other.EventBloomIndexFpp = EventBloomIndexFpp;
        other.EventForceInterval = EventForceInterval;
        other.EventPrimaryCleanupDelay = EventPrimaryCleanupDelay;
        other.EventSecondaryCleanupDelay = EventSecondaryCleanupDelay;
        other.EventSegmentSize = EventSegmentSize;
        other.EventSyncInterval = EventSyncInterval;
        other.EventValidationSegments = EventValidationSegments;
        other.EventMemoryMappedSegments = EventMemoryMappedSegments;
        other.EventsPerSegmentPrefetch = EventsPerSegmentPrefetch;
        other.SnapshotBloomIndexFpp = SnapshotBloomIndexFpp;
        other.SnapshotForceInterval = SnapshotForceInterval;
        other.SnapshotPrimaryCleanupDelay = SnapshotPrimaryCleanupDelay;
        other.SnapshotSecondaryCleanupDelay = SnapshotSecondaryCleanupDelay;
        other.SnapshotSegmentSize = SnapshotSegmentSize;
        other.SnapshotSyncInterval = SnapshotSyncInterval;
        other.SnapshotValidationSegments = SnapshotValidationSegments;
        other.SnapshotMemoryMappedSegments = SnapshotMemoryMappedSegments;
        other.QueryLimit = QueryLimit;
        other.NewPermitsTimeout = NewPermitsTimeout;
        other.BlacklistedSendAfter = BlacklistedSendAfter;
        other.MaxEventsPerTransaction = MaxEventsPerTransaction;
        other.EnterpriseDefaultIndexType = EnterpriseDefaultIndexType;
        other.ReadSequenceValidationStrategy = ReadSequenceValidationStrategy;
        other.EventUseMMapIndex = EventUseMMapIndex;
        other.EventForceCleanMMapIndex = EventForceCleanMMapIndex;
        other.EventAggregatePrefetch = EventAggregatePrefetch;
        other.EventAggegrateRetryAttempts = EventAggegrateRetryAttempts;
        other.EventAggregateRetryDelay = EventAggregateRetryDelay;
        other.EventLeaderRetryAttempts = EventLeaderRetryAttempts;
        other.EventLeaderRetryDelay = EventLeaderRetryDelay;
        other.EventProcessorPermitsCheck = EventProcessorPermitsCheck;
        other.CheckSequenceNrForSnapshots = CheckSequenceNrForSnapshots;
    }
}