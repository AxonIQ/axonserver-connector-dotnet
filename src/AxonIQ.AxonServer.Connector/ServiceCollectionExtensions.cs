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
//
//     public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services, IConfiguration configuration)
//     {
//         return services;
//     }
//     
//     public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services, Action<AxonServerConnectionFactoryOptions> configureOptions)
//     {
//         return services;
//     }
//     
//     public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services, AxonServerConnectionFactoryOptions configureOptions)
//     {
//         return services;
//     }
}