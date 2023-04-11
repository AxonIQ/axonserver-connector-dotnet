namespace AxonIQ.AxonServer.Connector;

public interface IEventProcessorInstructionHandler
{
    Task<bool> ReleaseSegmentAsync(SegmentId segment);
    Task<bool> SplitSegmentAsync(SegmentId segment);
    Task<bool> MergeSegmentAsync(SegmentId segment);
    Task PauseAsync();
    Task StartAsync();
}