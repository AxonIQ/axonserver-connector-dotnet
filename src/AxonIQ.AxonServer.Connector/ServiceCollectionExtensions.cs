using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AxonIQ.AxonServer.Connector;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds an Axon Server connection to the specified <paramref name="context"/>. 
    /// </summary>
    /// <param name="services">The service collection to add the connection to.</param>
    /// <param name="context">The context to connect to.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAxonServerConnection(this IServiceCollection services, Context context)
    {
        var builder = AxonServerConnectorOptions.For(ComponentName.GenerateRandomName());
        AddAxonServerConnectionCore(services, context, builder);
        return services;
    }

    /// <summary>
    /// Adds an Axon Server connection to the specified <paramref name="context"/> using the specified <paramref name="configuration"/> settings. 
    /// </summary>
    /// <param name="services">The service collection to add the connection to.</param>
    /// <param name="context">The context to connect to.</param>
    /// <param name="configuration">The configuration to read settings from.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAxonServerConnection(this IServiceCollection services,
        Context context,
        IConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        var builder = AxonServerConnectorOptions.FromConfiguration(configuration);
        AddAxonServerConnectionCore(services, context, builder);
        return services;
    }

    /// <summary>
    /// Adds an Axon Server connection to the specified <paramref name="context"/> using the specified <paramref name="options"/>. 
    /// </summary>
    /// <param name="services">The service collection to add the connection to.</param>
    /// <param name="context">The context to connect to.</param>
    /// <param name="options">The options to use.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAxonServerConnection(this IServiceCollection services,
        Context context,
        AxonServerConnectorOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        services.AddSingleton(_ => new AxonServerConnection(context, options));
        return services;
    }

    /// <summary>
    /// Adds an Axon Server connection to the specified <paramref name="context"/> using the specified <paramref name="configure"/> builder. 
    /// </summary>
    /// <param name="services">The service collection to add the connection to.</param>
    /// <param name="context">The context to connect to.</param>
    /// <param name="configure">The callback to configure options.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAxonServerConnection(this IServiceCollection services,
        Context context,
        Action<IAxonServerConnectorOptionsBuilder> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        var builder = AxonServerConnectorOptions.For(ComponentName.GenerateRandomName());
        configure(builder);
        AddAxonServerConnectionCore(services, context, builder);
        return services;
    }

    private static void AddAxonServerConnectionCore(
        IServiceCollection services,
        Context context,
        IAxonServerConnectorOptionsBuilder builder)
    {
        services.AddSingleton(sp =>
            new AxonServerConnection(
                context,
                builder
                    .WithLoggerFactory(sp.GetService<ILoggerFactory>() ?? new NullLoggerFactory())
                    .Build()));
    }
    
    /// <summary>
    /// Adds an Axon Server connection factory. 
    /// </summary>
    /// <param name="services">The service collection to add the connection factory to.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services)
    {
        var builder = AxonServerConnectorOptions.For(ComponentName.GenerateRandomName());
        AddAxonServerConnectionFactoryCore(services, builder);
        return services;
    }

    /// <summary>
    /// Adds an Axon Server connection factory using the specified <paramref name="configuration"/> settings. 
    /// </summary>
    /// <param name="services">The service collection to add the connection factory to.</param>
    /// <param name="configuration">The configuration to read settings from.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services,
        IConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        var builder = AxonServerConnectorOptions.FromConfiguration(configuration);
        AddAxonServerConnectionFactoryCore(services, builder);
        return services;
    }

    /// <summary>
    /// Adds an Axon Server connection factory using the specified <paramref name="options"/>. 
    /// </summary>
    /// <param name="services">The service collection to add the connection factory to.</param>
    /// <param name="options">The options to use.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services,
        AxonServerConnectorOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        services.AddSingleton(_ => new AxonServerConnectionFactory(options));
        return services;
    }

    /// <summary>
    /// Adds an Axon Server connection factory using the specified <paramref name="configure"/> builder. 
    /// </summary>
    /// <param name="services">The service collection to add the connection factory to.</param>
    /// <param name="configure">The callback to configure options.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services,
        Action<IAxonServerConnectorOptionsBuilder> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        var builder = AxonServerConnectorOptions.For(ComponentName.GenerateRandomName());
        configure(builder);
        AddAxonServerConnectionFactoryCore(services, builder);
        return services;
    }

    private static void AddAxonServerConnectionFactoryCore(
        IServiceCollection services,
        IAxonServerConnectorOptionsBuilder builder)
    {
        services.AddSingleton(sp =>
            new AxonServerConnectionFactory(
                builder
                    .WithLoggerFactory(sp.GetService<ILoggerFactory>() ?? new NullLoggerFactory())
                    .Build()));
    }
}