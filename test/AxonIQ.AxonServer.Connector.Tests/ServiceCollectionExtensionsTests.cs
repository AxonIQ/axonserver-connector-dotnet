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
            .GetRequiredSection(AxonServerConnectionFactoryConfiguration.DefaultSection);
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
            new ServiceCollection().AddAxonServerConnectionFactory((AxonServerConnectionFactoryOptions)null!));
    }

    [Fact]
    public void AddAxonServerConnectionFactoryWithOptionsHasExpectedResult()
    {
        var component = _fixture.Create<ComponentName>();
        var options = AxonServerConnectionFactoryOptions
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
                (Action<IAxonServerConnectionFactoryOptionsBuilder>)null!));
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
            InitialData = new KeyValuePair<string, string>[]
            {
                new(
                    AxonServerConnectionFactoryConfiguration.DefaultSection + ":" +
                    AxonServerConnectionFactoryConfiguration.ComponentName, component.ToString())
            }
        };
        var configuration = new ConfigurationRoot(new List<IConfigurationProvider>
            { new MemoryConfigurationProvider(source) });
        return configuration;
    }
}