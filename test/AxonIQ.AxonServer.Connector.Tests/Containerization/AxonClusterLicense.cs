namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public static class AxonClusterLicense
{
    public static string FromEnvironment()
    {
        var licensePath = Environment.GetEnvironmentVariable("AXONIQ_LICENSE");
        if (licensePath == null)
        {
            throw new InvalidOperationException(
                "The AXONIQ_LICENSE environment variable was not set. It is required if you want to interact with an embedded axon cluster.");
        }

        if (!File.Exists(licensePath))
        {
            throw new InvalidOperationException(
                $"The AXONIQ_LICENSE environment variable value refers to a file path that does not exist: {licensePath}");
        }

        return File.ReadAllText(licensePath);
    }
}