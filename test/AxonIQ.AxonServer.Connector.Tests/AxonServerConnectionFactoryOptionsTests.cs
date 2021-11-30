using System.Net;
using AutoFixture;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonServerConnectionFactoryOptionsTests
{
    private readonly IFixture _fixture;

    public AxonServerConnectionFactoryOptionsTests()
    {
        _fixture = new Fixture();
        _fixture.CustomizeComponentName();
        _fixture.CustomizeClientId();
    }

    [Fact]
    public void ForComponentNameReturnsExpectedResult()
    {
        var component = _fixture.Create<ComponentName>();

        var sut = AxonServerConnectionFactoryOptions.For(component);

        Assert.IsAssignableFrom<IAxonServerConnectionFactoryOptionsBuilder>(sut);

        var result = sut.Build();

        Assert.NotNull(result);
        Assert.Equal(component, result.ComponentName);
        Assert.StartsWith(result.ComponentName + "_", result.ClientInstanceId.ToString());
        Assert.Equal(AxonServerConnectionFactoryDefaults.RoutingServers, result.RoutingServers);
        Assert.Empty(result.ClientTags);
        Assert.Same(AxonServerAuthentication.None, result.Authentication);
    }

    [Fact]
    public void ForComponentNameAndClientInstanceIdReturnsExpectedResult()
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientId>();

        var sut = AxonServerConnectionFactoryOptions.For(component, clientInstance);

        Assert.IsAssignableFrom<IAxonServerConnectionFactoryOptionsBuilder>(sut);

        var result = sut.Build();

        Assert.NotNull(result);
        Assert.Equal(component, result.ComponentName);
        Assert.Equal(clientInstance, result.ClientInstanceId);
        Assert.Equal(AxonServerConnectionFactoryDefaults.RoutingServers, result.RoutingServers);
        Assert.Empty(result.ClientTags);
        Assert.Same(AxonServerAuthentication.None, result.Authentication);
    }

    private IAxonServerConnectionFactoryOptionsBuilder CreateSystemUnderTest()
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientId>();

        return AxonServerConnectionFactoryOptions.For(component, clientInstance);
    }

    [Fact]
    public void WithComponentNameHasExpectedResult()
    {
        var component = _fixture.Create<ComponentName>();

        var sut =
            CreateSystemUnderTest()
                .WithComponentName(component);

        Assert.IsAssignableFrom<IAxonServerConnectionFactoryOptionsBuilder>(sut);

        var result = sut.Build();

        Assert.Equal(component, result.ComponentName);
    }

    [Fact]
    public void WithClientInstanceIdHasExpectedResult()
    {
        var clientInstanceId = _fixture.Create<ClientId>();

        var sut =
            CreateSystemUnderTest()
                .WithClientInstanceId(clientInstanceId);

        Assert.IsAssignableFrom<IAxonServerConnectionFactoryOptionsBuilder>(sut);

        var result = sut.Build();

        Assert.Equal(clientInstanceId, result.ClientInstanceId);
    }

    [Fact]
    public void WithRoutingServersArrayCanNotBeNull()
    {
        var sut = CreateSystemUnderTest();

        Assert.Throws<ArgumentNullException>(() => sut.WithRoutingServers(null!));
    }

    [Fact]
    public void WithRoutingServersArrayHasExpectedResult()
    {
        var servers = _fixture.CreateMany<DnsEndPoint>(Random.Shared.Next(1, 5)).ToArray();

        var sut =
            CreateSystemUnderTest()
                .WithRoutingServers(servers);

        Assert.IsAssignableFrom<IAxonServerConnectionFactoryOptionsBuilder>(sut);

        var result = sut.Build();

        Assert.Equal(servers, result.RoutingServers);
    }

    [Fact]
    public void WithEmptyRoutingServersArrayHasExpectedResult()
    {
        var sut =
            CreateSystemUnderTest()
                .WithRoutingServers();

        Assert.IsAssignableFrom<IAxonServerConnectionFactoryOptionsBuilder>(sut);

        var result = sut.Build();

        Assert.Equal(AxonServerConnectionFactoryDefaults.RoutingServers, result.RoutingServers);
    }

    [Fact]
    public void WithRoutingServersEnumerableCanNotBeNull()
    {
        var sut = CreateSystemUnderTest();

        Assert.Throws<ArgumentNullException>(() => sut.WithRoutingServers((IEnumerable<DnsEndPoint>)null!));
    }

    [Fact]
    public void WithRoutingServersEnumerableHasExpectedResult()
    {
        IEnumerable<DnsEndPoint> servers = _fixture.CreateMany<DnsEndPoint>(Random.Shared.Next(1, 5)).ToArray();

        var sut =
            CreateSystemUnderTest()
                .WithRoutingServers(servers);

        Assert.IsAssignableFrom<IAxonServerConnectionFactoryOptionsBuilder>(sut);

        var result = sut.Build();

        Assert.Equal(servers, result.RoutingServers);
    }

    [Fact]
    public void WithEmptyRoutingServersEnumerableHasExpectedResult()
    {
        var sut =
            CreateSystemUnderTest()
                .WithRoutingServers(Enumerable.Empty<DnsEndPoint>());

        Assert.IsAssignableFrom<IAxonServerConnectionFactoryOptionsBuilder>(sut);

        var result = sut.Build();

        Assert.Equal(AxonServerConnectionFactoryDefaults.RoutingServers, result.RoutingServers);
    }

    [Fact]
    public void WithoutAuthenticationHasExpectedResult()
    {
        var sut =
            CreateSystemUnderTest()
                .WithoutAuthentication();

        Assert.IsAssignableFrom<IAxonServerConnectionFactoryOptionsBuilder>(sut);

        var result = sut.Build();

        Assert.Equal(AxonServerAuthentication.None, result.Authentication);
    }

    [Fact]
    public void WithAuthenticationTokenCanNotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CreateSystemUnderTest()
                .WithAuthenticationToken(null!));
    }

    [Fact]
    public void WithAuthenticationTokenHasExpectedResult()
    {
        var token = _fixture.Create<string>();

        var sut =
            CreateSystemUnderTest()
                .WithAuthenticationToken(token);

        Assert.IsAssignableFrom<IAxonServerConnectionFactoryOptionsBuilder>(sut);

        var result = sut.Build();

        var authentication = Assert.IsType<TokenBasedServerAuthentication>(result.Authentication);

        Assert.Equal(token, authentication.Token);
    }

    [Fact]
    public void WithClientTagsArrayCanNotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CreateSystemUnderTest()
                .WithClientTags(null!));
    }

    [Fact]
    public void WithClientTagsArrayHasExpectedResult()
    {
        var tags = _fixture.CreateMany<KeyValuePair<string, string>>(Random.Shared.Next(1, 5)).ToArray();

        var sut =
            CreateSystemUnderTest()
                .WithClientTags(tags);

        Assert.IsAssignableFrom<IAxonServerConnectionFactoryOptionsBuilder>(sut);

        var result = sut.Build();

        Assert.Equal(new Dictionary<string, string>(tags), result.ClientTags);
    }

    [Fact]
    public void WithClientTagsArrayOverwritesTags()
    {
        var writtenTags = _fixture
            .CreateMany<string>(Random.Shared.Next(1, 5))
            .Select(key => new KeyValuePair<string, string>(key, "1"))
            .ToArray();
        var overwriteTags = writtenTags
            .Select(tag => new KeyValuePair<string, string>(tag.Key, "2"))
            .ToArray();

        var sut =
            CreateSystemUnderTest()
                .WithClientTags(writtenTags)
                .WithClientTags(overwriteTags);

        Assert.IsAssignableFrom<IAxonServerConnectionFactoryOptionsBuilder>(sut);

        var result = sut.Build();

        Assert.Equal(new Dictionary<string, string>(overwriteTags), result.ClientTags);
    }

    [Fact]
    public void WithClientTagsEnumerableCanNotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CreateSystemUnderTest()
                .WithClientTags((IEnumerable<KeyValuePair<string, string>>)null!));
    }

    [Fact]
    public void WithClientTagsEnumerableHasExpectedResult()
    {
        IEnumerable<KeyValuePair<string, string>> tags =
            _fixture.CreateMany<KeyValuePair<string, string>>(Random.Shared.Next(1, 5)).ToArray();

        var sut =
            CreateSystemUnderTest()
                .WithClientTags(tags);

        Assert.IsAssignableFrom<IAxonServerConnectionFactoryOptionsBuilder>(sut);

        var result = sut.Build();

        Assert.Equal(new Dictionary<string, string>(tags), result.ClientTags);
    }

    [Fact]
    public void WithClientTagsEnumerableOverwritesTags()
    {
        IEnumerable<KeyValuePair<string, string>> writtenTags = _fixture
            .CreateMany<string>(Random.Shared.Next(1, 5))
            .Select(key => new KeyValuePair<string, string>(key, "1"))
            .ToArray();
        IEnumerable<KeyValuePair<string, string>> overwriteTags = writtenTags
            .Select(tag => new KeyValuePair<string, string>(tag.Key, "2"))
            .ToArray();

        var sut =
            CreateSystemUnderTest()
                .WithClientTags(writtenTags)
                .WithClientTags(overwriteTags);

        Assert.IsAssignableFrom<IAxonServerConnectionFactoryOptionsBuilder>(sut);

        var result = sut.Build();

        Assert.Equal(new Dictionary<string, string>(overwriteTags), result.ClientTags);
    }

    [Fact]
    public void WithClientTagKeyCanNotBeNull()
    {
        var value = _fixture.Create<string>();

        Assert.Throws<ArgumentNullException>(() =>
            CreateSystemUnderTest()
                .WithClientTag(null!, value));
    }

    [Fact]
    public void WithClientTagValueCanNotBeNull()
    {
        var key = _fixture.Create<string>();

        Assert.Throws<ArgumentNullException>(() =>
            CreateSystemUnderTest()
                .WithClientTag(key, null!));
    }

    [Fact]
    public void WithClientTagReturnsExpectedResult()
    {
        var key = _fixture.Create<string>();
        var value = _fixture.Create<string>();

        var sut =
            CreateSystemUnderTest()
                .WithClientTag(key, value);

        Assert.IsAssignableFrom<IAxonServerConnectionFactoryOptionsBuilder>(sut);
        var result = sut.Build();

        Assert.Equal(new Dictionary<string, string> { { key, value } }, result.ClientTags);
    }

    [Fact]
    public void WithClientTagOverwritesTag()
    {
        var key = _fixture.Create<string>();

        var sut =
            CreateSystemUnderTest()
                .WithClientTag(key, "1")
                .WithClientTag(key, "2");

        Assert.IsAssignableFrom<IAxonServerConnectionFactoryOptionsBuilder>(sut);
        var result = sut.Build();

        Assert.Equal(new Dictionary<string, string> { { key, "2" } }, result.ClientTags);
    }

    [Fact]
    public void FromConfigurationCanNotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() => AxonServerConnectionFactoryOptions.FromConfiguration(null!));
    }

    [Fact]
    public void FromConfigurationDefaultsToUnnamedComponentNameWhenComponentNameIsMissing()
    {
        var sut = AxonServerConnectionFactoryOptions.FromConfiguration(
            new ConfigurationRoot(new List<IConfigurationProvider>()));
        var result = sut.Build();
        Assert.StartsWith(ComponentName.Default.SuffixWith("_").ToString(),
            result.ComponentName.ToString());
        Assert.StartsWith(result.ComponentName.SuffixWith("_").ToString(), result.ClientInstanceId.ToString());
    }

    [Fact]
    public void FromMinimalConfigurationReturnsExpectedResult()
    {
        var component = _fixture.Create<ComponentName>();
        var source = new MemoryConfigurationSource
        {
            InitialData = new KeyValuePair<string, string>[]
            {
                new(AxonServerConnectionFactoryConfiguration.ComponentName, component.ToString())
            }
        };
        var configuration = new ConfigurationRoot(new List<IConfigurationProvider>
            { new MemoryConfigurationProvider(source) });

        var sut = AxonServerConnectionFactoryOptions.FromConfiguration(configuration);

        Assert.IsAssignableFrom<IAxonServerConnectionFactoryOptionsBuilder>(sut);

        var result = sut.Build();

        Assert.Equal(component, result.ComponentName);
        Assert.StartsWith(result.ComponentName + "_", result.ClientInstanceId.ToString());
        Assert.Equal(AxonServerConnectionFactoryDefaults.RoutingServers, result.RoutingServers);
        Assert.Empty(result.ClientTags);
        Assert.Same(AxonServerAuthentication.None, result.Authentication);
    }

    [Fact]
    public void FromConfigurationWithClientInstanceIdReturnsExpectedResult()
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientId>();
        var source = new MemoryConfigurationSource
        {
            InitialData = new KeyValuePair<string, string>[]
            {
                new(AxonServerConnectionFactoryConfiguration.ComponentName, component.ToString()),
                new(AxonServerConnectionFactoryConfiguration.ClientInstanceId, clientInstance.ToString())
            }
        };
        var configuration = new ConfigurationRoot(new List<IConfigurationProvider>
            { new MemoryConfigurationProvider(source) });

        var sut = AxonServerConnectionFactoryOptions.FromConfiguration(configuration);

        Assert.IsAssignableFrom<IAxonServerConnectionFactoryOptionsBuilder>(sut);

        var result = sut.Build();

        Assert.Equal(component, result.ComponentName);
        Assert.Equal(clientInstance, result.ClientInstanceId);
        Assert.Equal(AxonServerConnectionFactoryDefaults.RoutingServers, result.RoutingServers);
        Assert.Empty(result.ClientTags);
        Assert.Same(AxonServerAuthentication.None, result.Authentication);
    }

    //TODO: Extend with tests that cover obtaining all other options from configuration
}