using Io.Axoniq.Axonserver.Grpc.Command;

namespace AxonIQ.AxonServer.Connector;

public interface ICommandChannel
{
    Task<ICommandHandlerRegistration> RegisterCommandHandlerAsync(
        Func<Command, CancellationToken, Task<CommandResponse>> handler,
        LoadFactor loadFactor,
        params CommandName[] commandNames);

    Task<CommandResponse> SendCommandAsync(Command command, CancellationToken ct);
}