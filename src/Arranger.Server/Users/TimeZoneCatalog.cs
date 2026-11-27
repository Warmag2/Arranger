using System;
using System.Collections.Generic;
using System.Linq;
using Faemiyah.Arranger.Contracts.Accounts;
using Faemiyah.Arranger.Contracts.Users;

namespace Faemiyah.Arranger.Server.Users;

/// <summary>
/// The time zones users can choose from, as IANA IDs.
/// </summary>
public sealed class TimeZoneCatalog
{
    private readonly TimeProvider _timeProvider;
    private readonly IReadOnlyDictionary<string, TimeZoneInfo> _zones = LoadZones();

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
    /// The zones are loaded once, but the offsets are computed per call, since they change with daylight saving time.
    /// </remarks>
    /// <returns>The time zones.</returns>
    public IReadOnlyList<TimeZoneOption> GetTimeZones()
    {
        var now = _timeProvider.GetUtcNow();
        return _zones
            .Select(z => (Id: z.Key, Offset: z.Value.GetUtcOffset(now)))
            .OrderBy(z => z.Offset)
            .ThenBy(z => z.Id, StringComparer.Ordinal)
            .Select(z => new TimeZoneOption(z.Id, $"(UTC{FormatOffset(z.Offset)}) {z.Id.Replace('_', ' ')}"))
            .ToList();
    }

    private static string FormatOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return $"{sign}{offset.Duration():hh\\:mm}";
    }

    private static Dictionary<string, TimeZoneInfo> LoadZones()
    {
        var zones = new Dictionary<string, TimeZoneInfo>(StringComparer.Ordinal) { [AccountRules.DefaultTimeZoneId] = TimeZoneInfo.Utc };

        foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
        {
            // Windows lists Windows IDs; Linux already lists IANA IDs.
            string? id = zone.Id;
            if (!zone.HasIanaId && !TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out id))
            {
                continue;
            }

            if (id.Length <= AccountRules.TimeZoneIdMaxLength && !id.StartsWith("Etc/", StringComparison.Ordinal))
            {
                zones.TryAdd(id, zone);
            }
        }

        return zones;
    }
}
