namespace AxonIQ.AxonServer.Embedded;

public static class AxonClusterLicense
{
    private const string LicenseVariableName = "AXONIQ_LICENSE";
    private const string LicensePathVariableName = "AXONIQ_LICENSEPATH";

    public static string FromEnvironment()
    {
        var license = Environment.GetEnvironmentVariable(LicenseVariableName);
        var licensePath = Environment.GetEnvironmentVariable(LicensePathVariableName);
        if (license == null && licensePath == null)
        {
            throw new InvalidOperationException(
                $"Neither the {LicenseVariableName} nor the {LicensePathVariableName} environment variable was set. It is required that you set at least one if you want to interact with an embedded axon cluster.");
        }

        if (license != null)
        {
            return license;
        }

        if (!File.Exists(licensePath))
        {
            throw new InvalidOperationException(
                $"The {LicensePathVariableName} environment variable value refers to a file path that does not exist: {licensePath}");
        }

        return File.ReadAllText(licensePath);
    }
}