using System;
using Microsoft.AspNetCore.SignalR.Client;

namespace Faemiyah.Arranger.Client.Services;

/// <summary>
/// Reconnect forever, backing off to one attempt every 30 seconds. The server is expected to come back.
/// </summary>
public sealed class ForeverRetryPolicy : IRetryPolicy
{
    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
    ];

    /// <summary>
    /// The delay before the given attempt.
    /// </summary>
    /// <param name="previousAttempts">The number of attempts made so far.</param>
    /// <returns>The delay.</returns>
    public static TimeSpan GetDelay(long previousAttempts)
    {
        return Delays[(int)Math.Clamp(previousAttempts, 0, Delays.Length - 1)];
    }

    /// <inheritdoc />
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        ArgumentNullException.ThrowIfNull(retryContext);
        return GetDelay(retryContext.PreviousRetryCount);
    }
}
