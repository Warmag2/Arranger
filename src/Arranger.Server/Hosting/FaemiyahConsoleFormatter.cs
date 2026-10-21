using System;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Faemiyah.Arranger.Server.Hosting;

/// <summary>
/// A single-line console formatter producing <c>{ISO 8601 UTC time} - [{level}] - {category} - {message}</c>, with any
/// exception on the following lines. The same format as in BtDamageResolver.
/// </summary>
public sealed class FaemiyahConsoleFormatter : ConsoleFormatter
{
    /// <summary>
    /// The name under which the formatter is registered.
    /// </summary>
    public const string FormatterName = "faemiyah";

    /// <summary>
    /// Initializes a new instance of the <see cref="FaemiyahConsoleFormatter"/> class.
    /// </summary>
    public FaemiyahConsoleFormatter()
        : base(FormatterName)
    {
    }

    /// <inheritdoc />
    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        ArgumentNullException.ThrowIfNull(textWriter);
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        if (string.IsNullOrEmpty(message) && logEntry.Exception == null)
        {
            return;
        }

        textWriter.Write(DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
        textWriter.Write(" - [");
        textWriter.Write(logEntry.LogLevel);
        textWriter.Write("] - ");
        textWriter.Write(logEntry.Category);
        textWriter.Write(" - ");
        textWriter.Write(message);
        if (logEntry.Exception != null)
        {
            textWriter.Write(Environment.NewLine);
            textWriter.Write(logEntry.Exception);
        }

        textWriter.Write(Environment.NewLine);
    }
}
