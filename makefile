LICENSE = $(shell cat axoniq.license)

ci:
	dotnet tool restore
	dotnet restore
	dotnet build --configuration Release --no-restore
	dotnet test --configuration Release --no-build --no-restore test/AxonIQ.AxonServer.Connector.Tests
	dotnet test --configuration Release --no-build --no-restore test/AxonIQ.AxonServerIntegrationTests

cd:
	dotnet tool restore
	dotnet restore
	dotnet build --configuration Release --no-restore
	dotnet pack --configuration Release --no-build --no-restore --include-symbols --include-source src/AxonIQ.AxonServer.Connector/AxonIQ.AxonServer.Connector.csproj -o .artifacts/
	dotnet pack --configuration Release --no-build --no-restore --include-symbols --include-source src/AxonIQ.AxonServer.Embedded/AxonIQ.AxonServer.Embedded.csproj -o .artifacts/
	dotnet nuget push .artifacts/*.nupkg --api-key $(GITHUB_TOKEN) --source https://nuget.pkg.github.com/AxonIQ/index.json --skip-duplicate --no-symbols true
    	
install-license:
	dotnet user-secrets set "axoniq.license" "${LICENSE}" -p test/AxonIQ.AxonClusterIntegrationTests/AxonIQ.AxonClusterIntegrationTests.csproj