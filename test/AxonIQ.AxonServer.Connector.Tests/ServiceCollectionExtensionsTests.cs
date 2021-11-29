using AutoFixture;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class ServiceCollectionExtensionsTests
{
    private readonly Fixture _fixture;

    public ServiceCollectionExtensionsTests()
    {
        _fixture = new Fixture();
        _fixture.CustomizeComponentName();
    }
    
    [Fact]
    public void AddAxonServerConnectionFactoryHasExpectedResult()
    {
        var component = _fixture.Create<ComponentName>();
        var configuration = CreateMinimalConfiguration(component);
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        
        var result = services.AddAxonServerConnectionFactory();

        Assert.IsAssignableFrom<IServiceCollection>(result);

        var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<AxonServerConnectionFactory>();
        Assert.Equal(component, factory.ComponentName);
    }

    private static ConfigurationRoot CreateMinimalConfiguration(ComponentName component)
    {
        var source = new MemoryConfigurationSource
        {
            InitialData = new KeyValuePair<string, string>[]
            {
                new(AxonServerConnectionFactoryConfiguration.DefaultSection + ":" + AxonServerConnectionFactoryConfiguration.ComponentName, component.ToString())
            }
        };
        var configuration = new ConfigurationRoot(new List<IConfigurationProvider>
            { new MemoryConfigurationProvider(source) });
        return configuration;
    }
}