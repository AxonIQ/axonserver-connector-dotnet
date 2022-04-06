namespace AxonIQ.AxonServer.Connector;

public class ErrorCategory
{
    // Generic errors processing client request
    public static readonly ErrorCategory AuthenticationTokenMissing = new("AXONIQ-1000");
    public static readonly ErrorCategory AuthenticationInvalidToken = new("AXONIQ-1001");
    public static readonly ErrorCategory UnsupportedInstruction = new("AXONIQ-1002");
    public static readonly ErrorCategory InstructionAckError = new("AXONIQ-1003");
    public static readonly ErrorCategory InstructionExecutionError = new("AXONIQ-1004");

    //Event publishing errors
    public static readonly ErrorCategory InvalidEventSequence = new("AXONIQ-2000");

    public static readonly ErrorCategory NoEventStoreMasterAvailable = new("AXONIQ-2100"
    );

    public static readonly ErrorCategory EventPayloadTooLarge = new("AXONIQ-2001");

    //Communication errors
    public static readonly ErrorCategory ConnectionFailed = new("AXONIQ-3001");
    public static readonly ErrorCategory GrpcMessageTooLarge = new("AXONIQ-3002");

    // Command errors
    public static readonly ErrorCategory NoHandlerForCommand = new("AXONIQ-4000");
    public static readonly ErrorCategory CommandExecutionError = new("AXONIQ-4002");
    public static readonly ErrorCategory CommandDispatchError = new("AXONIQ-4003");
    public static readonly ErrorCategory ConcurrencyException = new("AXONIQ-4004");

    //Query errors
    public static readonly ErrorCategory NoHandlerForQuery = new("AXONIQ-5000");
    public static readonly ErrorCategory QueryExecutionError = new("AXONIQ-5001");
    public static readonly ErrorCategory QueryDispatchError = new("AXONIQ-5002");

    // Internal errors
    public static readonly ErrorCategory DatafileReadError = new("AXONIQ-9000");
    public static readonly ErrorCategory IndexReadError = new("AXONIQ-9001");
    public static readonly ErrorCategory DatafileWriteError = new("AXONIQ-9100");
    public static readonly ErrorCategory IndexWriteError = new("AXONIQ-9101");
    public static readonly ErrorCategory DirectoryCreationFailed = new("AXONIQ-9102");
    public static readonly ErrorCategory ValidationFailed = new("AXONIQ-9200");
    public static readonly ErrorCategory TransactionRolledBack = new("AXONIQ-9900");

    //Default
    public static readonly ErrorCategory Other = new("AXONIQ-0001");

    public static readonly IReadOnlyList<ErrorCategory> All = new[]
    {
        AuthenticationTokenMissing,
        AuthenticationInvalidToken,
        UnsupportedInstruction,
        InstructionAckError,
        InstructionExecutionError,
        InvalidEventSequence,
        NoEventStoreMasterAvailable,
        EventPayloadTooLarge,
        ConnectionFailed,
        GrpcMessageTooLarge,
        NoHandlerForCommand,
        CommandExecutionError,
        CommandDispatchError,
        ConcurrencyException,
        NoHandlerForQuery,
        QueryExecutionError,
        QueryDispatchError,
        DatafileReadError,
        IndexReadError,
        DatafileWriteError,
        IndexWriteError,
        DirectoryCreationFailed,
        ValidationFailed,
        TransactionRolledBack,
        Other
    };

    private readonly string _errorCode;

    private ErrorCategory(string errorCode)
    {
        _errorCode = errorCode;
    }
    
    private bool Equals(ErrorCategory instance) => instance._errorCode.Equals(_errorCode);
    public override bool Equals(object? obj) => obj is ErrorCategory instance && instance.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_errorCode);
    public override string ToString() => _errorCode;

    public static ErrorCategory Parse(string errorCode)
    {
        return All.FirstOrDefault(candidate => string.CompareOrdinal(candidate._errorCode, errorCode) == 0, Other);
    }
}