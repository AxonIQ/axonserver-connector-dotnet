/*
 * Copyright (c) 2022. AxonIQ
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

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