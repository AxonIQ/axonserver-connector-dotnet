namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class SystemFileLocations
{
    /// <summary>
    /// Path where (regular) events are stored as segmented files on disk. Default value is ./data directory.
    /// </summary>
    public string? EventStorage { get; set; }

    /// <summary>
    /// Path where Snapshot Events are stored as segmented files on disk. Default value is ./data directory.
    /// </summary>
    public string? SnapshotStorage { get; set; }

    /// <summary>
    /// Path where Axon Server's control database (axonserver-controldb) is created. Default value is ./data directory.
    /// </summary>
    public string? ControlDBPath { get; set; }

    /// <summary>
    /// Location where the control DB backups are created. Default value is ./ directory.
    /// </summary>
    public string? ControlDBBackupLocation { get; set; }

    /// <summary>
    /// Location where AxonServer creates its pid file. Default value is ./ directory.
    /// </summary>
    public string? PIDFileLocation { get; set; }

    /// <summary>
    /// Directory where the transaction logs for replication are stored. Default value is ./log directory.
    /// </summary>
    public string? ReplicationLogStorageFolder { get; set; }

    /// <summary>
    /// Directory where token is stored on startup. Default value ./security directory.
    /// </summary>
    public string? AccessControlTokenDir { get; set; }

    public string[] Serialize()
    {
        var properties = new List<string>();
        if (!string.IsNullOrEmpty(EventStorage))
        {
            properties.Add($"axoniq.axonserver.event.storage={EventStorage}");
        }

        if (!string.IsNullOrEmpty(SnapshotStorage))
        {
            properties.Add($"axoniq.axonserver.snapshot.storage={SnapshotStorage}");
        }

        if (!string.IsNullOrEmpty(ControlDBPath))
        {
            properties.Add($"axoniq.axonserver.controldb-path={ControlDBPath}");
        }

        if (!string.IsNullOrEmpty(ControlDBBackupLocation))
        {
            properties.Add($"axoniq.axonserver.controldb-backup-location={ControlDBBackupLocation}");
        }

        if (!string.IsNullOrEmpty(PIDFileLocation))
        {
            properties.Add($"axoniq.axonserver.pid-file-location={PIDFileLocation}");
        }

        if (!string.IsNullOrEmpty(ReplicationLogStorageFolder))
        {
            properties.Add($"axoniq.axonserver.replication.log-storage-folder={ReplicationLogStorageFolder}");
        }

        if (!string.IsNullOrEmpty(AccessControlTokenDir))
        {
            properties.Add($"axoniq.axonserver.accesscontrol.token-dir={AccessControlTokenDir}");
        }

        return properties.ToArray();
    }

    public void CopyTo(SystemFileLocations other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        other.EventStorage = EventStorage;
        other.SnapshotStorage = SnapshotStorage;
        other.ControlDBPath = ControlDBPath;
        other.ControlDBBackupLocation = ControlDBBackupLocation;
        other.PIDFileLocation = PIDFileLocation;
        other.ReplicationLogStorageFolder = ReplicationLogStorageFolder;
        other.AccessControlTokenDir = AccessControlTokenDir;
    }
}