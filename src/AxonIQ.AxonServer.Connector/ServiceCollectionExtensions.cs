using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AxonIQ.AxonServer.Connector;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services)
    {
        var options = AxonServerConnectionFactoryOptions
            .For(ComponentName.GenerateRandomName())
            .Build();
        services.AddSingleton(new AxonServerConnectionFactory(options));
        return services;
    }

    public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services,
        IConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        var options = AxonServerConnectionFactoryOptions
            .FromConfiguration(configuration)
            .Build();
        services.AddSingleton(new AxonServerConnectionFactory(options));
        return services;
    }

    public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services,
        AxonServerConnectionFactoryOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        services.AddSingleton(new AxonServerConnectionFactory(options));
        return services;
    }

    public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services,
        Action<IAxonServerConnectionFactoryOptionsBuilder> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        var builder = AxonServerConnectionFactoryOptions.For(ComponentName.GenerateRandomName());
        configure(builder);
        var options = builder.Build();
        services.AddSingleton(new AxonServerConnectionFactory(options));
        return services;
    }
}