using System;

namespace Faemiyah.Arranger.Contracts.Users;

/// <summary>
/// Change the signed-in user's settings.
/// </summary>
/// <param name="UserName">The new handle.</param>
/// <param name="TimeZoneId">The new IANA time zone.</param>
/// <param name="DefaultCurrency">The new ISO 4217 default currency.</param>
public sealed record UpdateUserSettingsRequest(string UserName, string TimeZoneId, string DefaultCurrency);
