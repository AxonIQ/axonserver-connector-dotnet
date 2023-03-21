namespace AxonIQ.AxonServer.Connector;

internal delegate ValueTask AxonActorStateChanged<in TState>(TState before, TState after);