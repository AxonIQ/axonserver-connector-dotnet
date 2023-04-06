using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AxonIQ.AxonServer.Connector;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services)
    {
        var builder = AxonServerConnectionFactoryOptions.For(ComponentName.GenerateRandomName());
        AddAxonServerConnectionFactoryCore(services, builder);
        return services;
    }

    public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services,
        IConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        var builder = AxonServerConnectionFactoryOptions.FromConfiguration(configuration);
        AddAxonServerConnectionFactoryCore(services, builder);
        return services;
    }

    public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services,
        AxonServerConnectionFactoryOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        services.AddSingleton(sp => new AxonServerConnectionFactory(options));
        return services;
    }

    public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services,
        Action<IAxonServerConnectionFactoryOptionsBuilder> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        var builder = AxonServerConnectionFactoryOptions.For(ComponentName.GenerateRandomName());
        configure(builder);
        AddAxonServerConnectionFactoryCore(services, builder);
        return services;
    }

    private static void AddAxonServerConnectionFactoryCore(
        IServiceCollection services,
        IAxonServerConnectionFactoryOptionsBuilder builder)
    {
        services.AddSingleton(sp =>
            new AxonServerConnectionFactory(
                builder
                    .WithLoggerFactory(sp.GetService<ILoggerFactory>() ?? new NullLoggerFactory())
                    .Build()));
    }
}