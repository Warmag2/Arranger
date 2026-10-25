using System;
using System.Collections.Generic;

namespace Faemiyah.Arranger.Contracts.Users;

/// <summary>
/// A selectable time zone.
/// </summary>
/// <param name="Id">The IANA time zone ID.</param>
/// <param name="Name">The display name, with the current UTC offset.</param>
public sealed record TimeZoneOption(string Id, string Name)
{
    /// <summary>
    /// Combo box options (name to ID) for the given time zones, ordered by name.
    /// </summary>
    /// <param name="zones">The time zones.</param>
    /// <returns>The options.</returns>
    public static SortedDictionary<string, string> Options(IEnumerable<TimeZoneOption> zones)
    {
        var options = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var zone in zones)
        {
            options[zone.Name] = zone.Id;
        }

        return options;
    }
}
