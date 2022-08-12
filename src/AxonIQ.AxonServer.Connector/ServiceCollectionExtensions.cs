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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AxonIQ.AxonServer.Connector;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAxonServerConnectionFactory(this IServiceCollection services)
    {
        var options = AxonServerConnectionFactoryOptions
            .For(ComponentName.GenerateRandomName())
            .Build();
        services.AddSingleton(sp => new AxonServerConnectionFactory(options));
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
        services.AddSingleton(sp => new AxonServerConnectionFactory(options));
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
        var options = builder.Build();
        services.AddSingleton(sp => new AxonServerConnectionFactory(options));
        return services;
    }
}