namespace AxonIQ.AxonServer.Connector;

public interface IEventProcessorInstructionHandler
{
    Task<bool> ReleaseSegment(SegmentId segment);
    Task<bool> SplitSegment(SegmentId segment);
    Task<bool> MergeSegment(SegmentId segment);
    Task Pause();
    Task Start();
}