using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AxonIQ.AxonServer.Connector;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var section = configuration.GetRequiredSection(AxonServerConnectionFactoryConfiguration.DefaultSection);
            var options = AxonServerConnectionFactoryOptions
                .FromConfiguration(section)
                .Build();
            return new AxonServerConnectionFactory(options);
        });
        return services;
    }
    
    public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration == null) 
            throw new ArgumentNullException(nameof(configuration));
        var options = AxonServerConnectionFactoryOptions
            .FromConfiguration(configuration)
            .Build();
        services.AddSingleton(new AxonServerConnectionFactory(options));
        return services;
    }
    
    public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services, AxonServerConnectionFactoryOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        services.AddSingleton(new AxonServerConnectionFactory(options));
        return services;
    }

    public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services, Action<IAxonServerConnectionFactoryOptionsBuilder> configure)
    {
        if (configure == null) 
            throw new ArgumentNullException(nameof(configure));
        
        services.AddSingleton(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var section = configuration.GetRequiredSection(AxonServerConnectionFactoryConfiguration.DefaultSection);
            var builder = AxonServerConnectionFactoryOptions
                .FromConfiguration(section);
                configure(builder);
            var options = builder.Build();
            return new AxonServerConnectionFactory(options);
        });
        return services;
    }
}