namespace AxonIQ.AxonServer.Connector;

internal record QueryExecution(
    InstructionId QueryId,
    IFlowControl FlowControl,
    CancellationTokenSource CancellationTokenSource);