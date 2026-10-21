using System.Diagnostics.CodeAnalysis;

namespace Faemiyah.Arranger.Client.Services;

/// <summary>
/// The outcome of an API call which returns a value.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
/// <param name="Value">The value on success, otherwise <b>null</b>.</param>
/// <param name="Error">The error message to show on failure, otherwise <b>null</b>.</param>
public sealed record ApiResult<T>(T? Value, string? Error)
    where T : class
{
    /// <summary>
    /// Whether the call succeeded.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error == null;
}
