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
        _fixture.CustomizeContext();
    }

    [Fact]
    public void AddAxonServerConnectionHasExpectedResult()
    {
        var services = new ServiceCollection();
        var context = _fixture.Create<Context>();

        var result = services.AddAxonServerConnection(context);

        Assert.IsAssignableFrom<IServiceCollection>(result);

        var provider = services.BuildServiceProvider();

        var connection = provider.GetRequiredService<AxonServerConnection>();
        Assert.Equal(context, connection.Context);
        Assert.StartsWith(ComponentName.Default.SuffixWith("_").ToString(),
            connection.ClientIdentity.ComponentName.ToString());
        Assert.StartsWith(connection.ClientIdentity.ComponentName.ToString(),
            connection.ClientIdentity.ClientInstanceId.ToString());
    }
    
    [Fact]
    public void AddAxonServerConnectionWithConfigurationCanNotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ServiceCollection().AddAxonServerConnection(_fixture.Create<Context>(), (IConfiguration)null!));
    }
    
    [Fact]
    public void AddAxonServerConnectionWithConfigurationHasExpectedResult()
    {
        var component = _fixture.Create<ComponentName>();
        var configuration = CreateMinimalConfiguration(component)
            .GetRequiredSection(AxonServerConnectorConfiguration.DefaultSection);
        var services = new ServiceCollection();

        var context = _fixture.Create<Context>();

        var result = services.AddAxonServerConnection(context, configuration);

        Assert.IsAssignableFrom<IServiceCollection>(result);

        var provider = services.BuildServiceProvider();

        var connection = provider.GetRequiredService<AxonServerConnection>();
        Assert.Equal(context, connection.Context);
        Assert.Equal(component, connection.ClientIdentity.ComponentName);
        Assert.StartsWith(connection.ClientIdentity.ComponentName.ToString(),
            connection.ClientIdentity.ClientInstanceId.ToString());
    }
    
    [Fact]
    public void AddAxonServerConnectionWithoutConfigurationHasExpectedResult()
    {
        var configuration = new ConfigurationRoot(new List<IConfigurationProvider>());
        var services = new ServiceCollection();

        var context = _fixture.Create<Context>();

        var result = services.AddAxonServerConnection(context, configuration);

        Assert.IsAssignableFrom<IServiceCollection>(result);

        var provider = services.BuildServiceProvider();

        var connection = provider.GetRequiredService<AxonServerConnection>();
        Assert.Equal(context, connection.Context);
        Assert.StartsWith(ComponentName.Default.SuffixWith("_").ToString(),
            connection.ClientIdentity.ComponentName.ToString());
        Assert.StartsWith(connection.ClientIdentity.ComponentName.ToString(),
            connection.ClientIdentity.ClientInstanceId.ToString());
    }

    [Fact]
    public void AddAxonServerConnectionWithOptionsCanNotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ServiceCollection().AddAxonServerConnection(_fixture.Create<Context>(), (AxonServerConnectorOptions)null!));
    }

    [Fact]
    public void AddAxonServerConnectionWithOptionsHasExpectedResult()
    {
        var component = _fixture.Create<ComponentName>();
        var options = AxonServerConnectorOptions
            .For(component)
            .Build();
        var services = new ServiceCollection();
        
        var context = _fixture.Create<Context>();

        var result = services.AddAxonServerConnection(context, options);

        Assert.IsAssignableFrom<IServiceCollection>(result);

        var provider = services.BuildServiceProvider();

        var connection = provider.GetRequiredService<AxonServerConnection>();
        Assert.Equal(context, connection.Context);
        Assert.Equal(component, connection.ClientIdentity.ComponentName);
        Assert.StartsWith(connection.ClientIdentity.ComponentName.ToString(),
            connection.ClientIdentity.ClientInstanceId.ToString());
    }

    [Fact]
    public void AddAxonServerConnectionWithOptionsBuilderCanNotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ServiceCollection().AddAxonServerConnection(
                _fixture.Create<Context>(),
                (Action<IAxonServerConnectorOptionsBuilder>)null!));
    }

    [Fact]
    public void AddAxonServerConnectionWithOptionsBuilderHasExpectedResult()
    {
        var services = new ServiceCollection();

        var context = _fixture.Create<Context>();
        
        var signal = new Signal();
        var result = services.AddAxonServerConnection(context, _ => { signal.Signaled = true; });

        Assert.IsAssignableFrom<IServiceCollection>(result);

        var provider = services.BuildServiceProvider();

        var connection = provider.GetRequiredService<AxonServerConnection>();
        Assert.Equal(context, connection.Context);
        Assert.True(signal.Signaled);
        Assert.StartsWith(ComponentName.Default.SuffixWith("_").ToString(),
            connection.ClientIdentity.ComponentName.ToString());
        Assert.StartsWith(connection.ClientIdentity.ComponentName.ToString(),
            connection.ClientIdentity.ClientInstanceId.ToString());
    }
    
    [Fact]
    public void AddAxonServerConnectionFactoryHasExpectedResult()
    {
        var services = new ServiceCollection();

        var result = services.AddAxonServerConnectionFactory();

        Assert.IsAssignableFrom<IServiceCollection>(result);

        var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<AxonServerConnectionFactory>();
        Assert.StartsWith(ComponentName.Default.SuffixWith("_").ToString(),
            factory.ClientIdentity.ComponentName.ToString());
        Assert.StartsWith(factory.ClientIdentity.ComponentName.ToString(),
            factory.ClientIdentity.ClientInstanceId.ToString());
    }

    [Fact]
    public void AddAxonServerConnectionFactoryWithConfigurationCanNotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ServiceCollection().AddAxonServerConnectionFactory((IConfiguration)null!));
    }

    [Fact]
    public void AddAxonServerConnectionFactoryWithConfigurationHasExpectedResult()
    {
        var component = _fixture.Create<ComponentName>();
        var configuration = CreateMinimalConfiguration(component)
            .GetRequiredSection(AxonServerConnectorConfiguration.DefaultSection);
        var services = new ServiceCollection();

        var result = services.AddAxonServerConnectionFactory(configuration);

        Assert.IsAssignableFrom<IServiceCollection>(result);

        var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<AxonServerConnectionFactory>();
        Assert.Equal(component, factory.ClientIdentity.ComponentName);
        Assert.StartsWith(factory.ClientIdentity.ComponentName.ToString(),
            factory.ClientIdentity.ClientInstanceId.ToString());
    }

    [Fact]
    public void AddAxonServerConnectionFactoryWithoutConfigurationHasExpectedResult()
    {
        var configuration = new ConfigurationRoot(new List<IConfigurationProvider>());
        var services = new ServiceCollection();

        var result = services.AddAxonServerConnectionFactory(configuration);

        Assert.IsAssignableFrom<IServiceCollection>(result);

        var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<AxonServerConnectionFactory>();
        Assert.StartsWith(ComponentName.Default.SuffixWith("_").ToString(),
            factory.ClientIdentity.ComponentName.ToString());
        Assert.StartsWith(factory.ClientIdentity.ComponentName.ToString(),
            factory.ClientIdentity.ClientInstanceId.ToString());
    }

    [Fact]
    public void AddAxonServerConnectionFactoryWithOptionsCanNotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ServiceCollection().AddAxonServerConnectionFactory((AxonServerConnectorOptions)null!));
    }

    [Fact]
    public void AddAxonServerConnectionFactoryWithOptionsHasExpectedResult()
    {
        var component = _fixture.Create<ComponentName>();
        var options = AxonServerConnectorOptions
            .For(component)
            .Build();
        var services = new ServiceCollection();

        var result = services.AddAxonServerConnectionFactory(options);

        Assert.IsAssignableFrom<IServiceCollection>(result);

        var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<AxonServerConnectionFactory>();
        Assert.Equal(component, factory.ClientIdentity.ComponentName);
        Assert.StartsWith(factory.ClientIdentity.ComponentName.ToString(),
            factory.ClientIdentity.ClientInstanceId.ToString());
    }

    [Fact]
    public void AddAxonServerConnectionFactoryWithOptionsBuilderCanNotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ServiceCollection().AddAxonServerConnectionFactory(
                (Action<IAxonServerConnectorOptionsBuilder>)null!));
    }

    [Fact]
    public void AddAxonServerConnectionFactoryWithOptionsBuilderHasExpectedResult()
    {
        var services = new ServiceCollection();

        var signal = new Signal();
        var result = services.AddAxonServerConnectionFactory(_ => { signal.Signaled = true; });

        Assert.IsAssignableFrom<IServiceCollection>(result);

        var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<AxonServerConnectionFactory>();
        Assert.True(signal.Signaled);
        Assert.StartsWith(ComponentName.Default.SuffixWith("_").ToString(),
            factory.ClientIdentity.ComponentName.ToString());
        Assert.StartsWith(factory.ClientIdentity.ComponentName.ToString(),
            factory.ClientIdentity.ClientInstanceId.ToString());
    }

    private class Signal
    {
        public bool Signaled { get; set; }
    }

    private static ConfigurationRoot CreateMinimalConfiguration(ComponentName component)
    {
        var source = new MemoryConfigurationSource
        {
            InitialData = new KeyValuePair<string, string?>[]
            {
                new(
                    AxonServerConnectorConfiguration.DefaultSection + ":" +
                    AxonServerConnectorConfiguration.ComponentName, component.ToString())
            }
        };
        var configuration = new ConfigurationRoot(new List<IConfigurationProvider>
            { new MemoryConfigurationProvider(source) });
        return configuration;
    }
}