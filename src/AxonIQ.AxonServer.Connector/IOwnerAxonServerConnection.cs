using Grpc.Core;

namespace AxonIQ.AxonServer.Connector;

internal interface IOwnerAxonServerConnection
{
    ClientIdentity ClientIdentity { get; }

    Context Context { get;  }
    
    CallInvoker CallInvoker { get; }

    ValueTask ReconnectAsync();

    ValueTask CheckReadinessAsync();
}