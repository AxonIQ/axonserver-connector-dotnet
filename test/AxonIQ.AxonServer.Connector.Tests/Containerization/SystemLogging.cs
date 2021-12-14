namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class SystemLogging
{
    /// <summary>
    /// Change the logging level for specific packages or classes (e.g. logging.level.io.axoniq.axonserver = INFO). Default value is WARN level for all packages.
    /// </summary>
    public KeyValuePair<string,string>[]? LogLevels { get; set; }
    /// <summary>
    /// File name where log entries should be written to. Names can be an exact location or relative to the current directory. (e.g. logging.file.name = messaging.log). Default value is stdout. 
    /// </summary>
    public string? LoggingFileName { get; set; }
    /// <summary>
    /// Location where log files should be created. Names can be an exact location or relative to the current directory (e.g. logging.path = /var/log). Default value is stdout.
    /// </summary>
    public string? LoggingPath { get; set; }
    
    public string[] Serialize()
    {
        var properties = new List<string>();
        if (LogLevels != null && LogLevels.Length != 0)
        {
            foreach (var (package, level) in LogLevels)
            {
                properties.Add($"logging.level.{package}={level}");    
            }
        }
        
        if (!string.IsNullOrEmpty(LoggingFileName))
        {
            properties.Add($"logging.file.name={LoggingFileName}");
        }

        if (!string.IsNullOrEmpty(LoggingPath))
        {
            properties.Add($"logging.path={LoggingPath}");
        }

        return properties.ToArray();
    }

    public void CopyTo(SystemLogging other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        other.LogLevels = LogLevels?.ToArray();
        other.LoggingPath = LoggingPath;
        other.LoggingFileName = LoggingFileName;
    }
}