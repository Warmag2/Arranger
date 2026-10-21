using System;
using System.Collections.Generic;
using System.Linq;
using Faemiyah.Arranger.Contracts.Users;

namespace Faemiyah.Arranger.Server.Users;

/// <summary>
/// The time zones users can choose from, as IANA IDs.
/// </summary>
public sealed class TimeZoneCatalog
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneCatalog"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    public TimeZoneCatalog(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Get the time zones with their current offsets, ordered by offset and then ID.
    /// </summary>
    /// <remarks>
    /// Computed per call, since offsets change with daylight saving time.
    /// </remarks>
    /// <returns>The time zones.</returns>
    public IReadOnlyList<TimeZoneOption> GetTimeZones()
    {
        var now = _timeProvider.GetUtcNow();
        var zones = new Dictionary<string, TimeSpan>(StringComparer.Ordinal) { ["UTC"] = TimeSpan.Zero };

        foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
        {
            // Windows lists Windows IDs; Linux already lists IANA IDs.
            string? id = zone.Id;
            if (!zone.HasIanaId && !TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out id))
            {
                continue;
            }

            if (id.Length <= Contracts.Accounts.AccountRules.TimeZoneIdMaxLength && !id.StartsWith("Etc/", StringComparison.Ordinal))
            {
                zones.TryAdd(id, zone.GetUtcOffset(now));
            }
        }

        return zones
            .OrderBy(z => z.Value)
            .ThenBy(z => z.Key, StringComparer.Ordinal)
            .Select(z => new TimeZoneOption(z.Key, $"(UTC{FormatOffset(z.Value)}) {z.Key.Replace('_', ' ')}"))
            .ToList();
    }

    private static string FormatOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return $"{sign}{offset.Duration():hh\\:mm}";
    }
}
