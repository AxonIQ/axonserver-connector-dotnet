LICENSE = $(shell cat axoniq.license)

ci:
	dotnet test test/AxonIQ.AxonServer.Connector.Tests

install-license:
	dotnet user-secrets set "axoniq.license" "${LICENSE}" -p test/AxonIQ.AxonServer.Connector.Tests/AxonIQ.AxonServer.Connector.Tests.csproj