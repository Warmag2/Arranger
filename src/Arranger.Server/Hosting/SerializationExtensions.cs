using System;
using System.Text.Json;
using Faemiyah.Arranger.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Serialization;

namespace Faemiyah.Arranger.Server.Hosting;

/// <summary>
/// Orleans silo and JSON serialization setup, configured the same way as in BtDamageResolver.
/// </summary>
public static class SerializationExtensions
{
    /// <summary>
    /// Host an Orleans silo in this process, with the Arranger grains and serializer. The ports can be changed with
    /// <c>Orleans:SiloPort</c> and <c>Orleans:GatewayPort</c>.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The builder for further processing.</returns>
    public static WebApplicationBuilder AddArrangerOrleans(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var siloPort = builder.Configuration.GetValue("Orleans:SiloPort", EndpointOptions.DEFAULT_SILO_PORT);
        var gatewayPort = builder.Configuration.GetValue("Orleans:GatewayPort", EndpointOptions.DEFAULT_GATEWAY_PORT);
        builder.Host.UseOrleans(siloBuilder =>
        {
            siloBuilder.UseLocalhostClustering(siloPort, gatewayPort);
            siloBuilder.Services.AddArrangerSerializer();
        });

        return builder;
    }

    /// <summary>
    /// Serialize all Faemiyah.Arranger types with System.Text.Json in Orleans.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for further processing.</returns>
    public static IServiceCollection AddArrangerSerializer(this IServiceCollection services)
    {
        return services.AddSerializer(serializerBuilder => serializerBuilder.AddJsonSerializer(
            isSupported: IsArrangerType,
            jsonSerializerOptions: JsonConfiguration.CreateJsonSerializerOptions()));
    }

    /// <summary>
    /// Apply the Faemiyah JSON defaults to the injectable options and to minimal API responses.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for further processing.</returns>
    public static IServiceCollection ConfigureArrangerJson(this IServiceCollection services)
    {
        services.Configure<JsonSerializerOptions>(JsonConfiguration.ApplyDefaultJsonSerializerOptions);
        services.ConfigureHttpJsonOptions(options => JsonConfiguration.ApplyDefaultJsonSerializerOptions(options.SerializerOptions));
        return services;
    }

    /// <summary>
    /// Whether Orleans should serialize the given type with System.Text.Json.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns><b>true</b> for types in the Faemiyah.Arranger namespaces.</returns>
    public static bool IsArrangerType(Type type)
    {
        return type.Namespace != null && type.Namespace.StartsWith(JsonConfiguration.SerializedNamespacePrefix, StringComparison.Ordinal);
    }
}
