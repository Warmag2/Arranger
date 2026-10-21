using System.Text.Json;
using System.Text.Json.Serialization;

namespace Faemiyah.Arranger.Contracts;

/// <summary>
/// The Faemiyah default JSON settings, shared by Orleans, the HTTP API, SignalR and the WebAssembly client.
/// </summary>
public static class JsonConfiguration
{
    /// <summary>
    /// The namespace prefix of every type Orleans serializes with System.Text.Json.
    /// </summary>
    public const string SerializedNamespacePrefix = "Faemiyah.Arranger";

    /// <summary>
    /// Build a new <see cref="JsonSerializerOptions"/> instance with the Faemiyah defaults applied.
    /// </summary>
    /// <returns>The configured options instance.</returns>
    public static JsonSerializerOptions CreateJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions();
        ApplyDefaultJsonSerializerOptions(options);
        return options;
    }

    /// <summary>
    /// Apply the Faemiyah default JSON serializer settings to an existing <see cref="JsonSerializerOptions"/> instance.
    /// </summary>
    /// <param name="options">The options instance to mutate.</param>
    public static void ApplyDefaultJsonSerializerOptions(JsonSerializerOptions options)
    {
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.PropertyNameCaseInsensitive = false;
        options.Converters.Add(new JsonStringEnumConverter());
    }
}
