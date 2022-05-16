using Io.Axoniq.Axonserver.Grpc.Command;

namespace AxonIQ.AxonServer.Connector;

public interface ICommandChannel
{
    Task<ICommandHandlerRegistration> RegisterCommandHandler(
        Func<Command, CancellationToken, Task<CommandResponse>> handler,
        LoadFactor loadFactor,
        params CommandName[] commandNames);

    Task<CommandResponse> SendCommand(Command command, CancellationToken ct);
}