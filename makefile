LICENSE = $(shell cat axoniq.license)

ci:
	dotnet tool restore
	dotnet restore
	dotnet build --configuration Release --no-restore
	dotnet test --configuration Release --no-build --no-restore test/AxonIQ.AxonServer.Connector.Tests --logger "trx;logfilename=connector_tests.trx"
	# dotnet test --configuration Release --no-build --no-restore test/AxonIQ.AxonServerIntegrationTests --filter "Surface=AdminChannel" --logger "trx;logfilename=server_integration_tests_admin_channel.trx" 
	dotnet test --configuration Release --no-build --no-restore test/AxonIQ.AxonServerIntegrationTests --filter "Surface=ControlChannel" --logger "trx;logfilename=server_integration_tests_control_channel.trx"
	dotnet test --configuration Release --no-build --no-restore test/AxonIQ.AxonServerIntegrationTests --filter "Surface=CommandChannel" --logger "trx;logfilename=server_integration_tests_command_channel.trx"
	dotnet test --configuration Release --no-build --no-restore test/AxonIQ.AxonServerIntegrationTests --filter "Surface=EventChannel" --logger "trx;logfilename=server_integration_tests_event_channel.trx"
	dotnet test --configuration Release --no-build --no-restore test/AxonIQ.AxonServerIntegrationTests --filter "Surface=QueryChannel" --logger "trx;logfilename=server_integration_tests_query_channel.trx"
	dotnet test --configuration Release --no-build --no-restore test/AxonIQ.AxonServerIntegrationTests --filter "Surface=HearbeatChannel" --logger "trx;logfilename=server_integration_tests_heartbeat_channel.trx"

cd:
	dotnet tool restore
	dotnet restore
	dotnet build --configuration Release --no-restore
	dotnet pack --configuration Release --no-build --no-restore --include-symbols --include-source src/AxonIQ.AxonServer.Connector/AxonIQ.AxonServer.Connector.csproj -o .artifacts/
	dotnet pack --configuration Release --no-build --no-restore --include-symbols --include-source src/AxonIQ.AxonServer.Embedded/AxonIQ.AxonServer.Embedded.csproj -o .artifacts/
	dotnet nuget push .artifacts/*.nupkg --api-key $(NUGET_APIKEY) --source https://api.nuget.org/v3/index.json --skip-duplicate --no-symbols
	dotnet nuget push .artifacts/*.nupkg --api-key $(GITHUB_TOKEN) --source https://nuget.pkg.github.com/AxonIQ/index.json --skip-duplicate --no-symbols
    	
install-license:
	dotnet user-secrets set "axoniq.license" "${LICENSE}" -p test/AxonIQ.AxonClusterIntegrationTests/AxonIQ.AxonClusterIntegrationTests.csproj
	
remove-dangling-containers:
	docker rm -f $(docker ps -a --filter "name=axonserver-" -q)