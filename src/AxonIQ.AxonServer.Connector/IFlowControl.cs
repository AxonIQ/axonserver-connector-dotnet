namespace AxonIQ.AxonServer.Connector;

// internal interface IFlowControl
// {
//     void Request(long count);
// }

internal interface IFlowControl
{
    void Request(long count);

    void Cancel();
}