using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Faemiyah.Arranger.Server.Hosting;

/// <summary>
/// Console logging setup. Levels are configured under <c>Logging:LogLevel</c> as usual.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Log only to the console, in the <see cref="FaemiyahConsoleFormatter"/> format.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The builder for further processing.</returns>
    public static WebApplicationBuilder AddArrangerLogging(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsoleFormatter<FaemiyahConsoleFormatter, ConsoleFormatterOptions>();
        builder.Logging.AddConsole(options => options.FormatterName = FaemiyahConsoleFormatter.FormatterName);
        return builder;
    }
}
