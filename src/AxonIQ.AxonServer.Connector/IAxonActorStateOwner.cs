namespace AxonIQ.AxonServer.Connector;

internal interface IAxonActorStateOwner<TState>
{
    TState State { get; set; }
}