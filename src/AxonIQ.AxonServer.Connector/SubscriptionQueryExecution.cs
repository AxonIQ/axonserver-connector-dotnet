namespace AxonIQ.AxonServer.Connector;

internal record SubscriptionQueryExecution(
    SubscriptionId SubscriptionId,
    CancellationTokenSource CancellationTokenSource);