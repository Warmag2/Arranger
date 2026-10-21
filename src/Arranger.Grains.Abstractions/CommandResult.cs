using System.Text.Json.Serialization;
using Faemiyah.Arranger.Contracts.Arrangements;

namespace Faemiyah.Arranger.Grains.Abstractions;

/// <summary>
/// The outcome of an arrangement grain call.
/// </summary>
/// <param name="Error">The error, or <see cref="CommandError.None"/> on success.</param>
/// <param name="Message">A human-readable error description, or <b>null</b>.</param>
/// <param name="Snapshot">The arrangement state after the command, or <b>null</b> when there is none.</param>
public sealed record CommandResult(CommandError Error, string? Message, ArrangementSnapshot? Snapshot)
{
    /// <summary>
    /// Whether the command succeeded.
    /// </summary>
    [JsonIgnore]
    public bool IsSuccess => Error == CommandError.None;

    /// <summary>
    /// A successful result.
    /// </summary>
    /// <param name="snapshot">The arrangement state after the command, or <b>null</b> after deletion.</param>
    /// <returns>The result.</returns>
    public static CommandResult Success(ArrangementSnapshot? snapshot)
    {
        return new CommandResult(CommandError.None, null, snapshot);
    }

    /// <summary>
    /// A failed result.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="message">A human-readable error description.</param>
    /// <returns>The result.</returns>
    public static CommandResult Failure(CommandError error, string message)
    {
        return new CommandResult(error, message, null);
    }
}
