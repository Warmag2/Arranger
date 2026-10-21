namespace Faemiyah.Arranger.Contracts.Users;

/// <summary>
/// A selectable time zone.
/// </summary>
/// <param name="Id">The IANA time zone ID.</param>
/// <param name="Name">The display name, with the current UTC offset.</param>
public sealed record TimeZoneOption(string Id, string Name);
