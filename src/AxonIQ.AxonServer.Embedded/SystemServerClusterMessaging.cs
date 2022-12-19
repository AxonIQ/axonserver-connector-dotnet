namespace AxonIQ.AxonServer.Embedded;

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