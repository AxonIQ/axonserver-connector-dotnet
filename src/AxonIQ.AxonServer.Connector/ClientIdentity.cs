namespace AxonIQ.AxonServer.Connector;

public record ClientIdentity(ComponentName ComponentName, ClientInstanceId ClientInstanceId,
    IReadOnlyDictionary<string, string> ClientTags, Version Version);