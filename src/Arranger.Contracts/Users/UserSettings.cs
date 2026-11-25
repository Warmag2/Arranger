using System;

namespace Faemiyah.Arranger.Contracts.Users;

/// <summary>
/// The signed-in user's own settings.
/// </summary>
/// <param name="UserId">The user ID.</param>
/// <param name="UserName">The username.</param>
/// <param name="TimeZoneId">The IANA time zone times are shown in.</param>
/// <param name="DefaultCurrency">The ISO 4217 base currency for new arrangements.</param>
/// <param name="PictureVersion">The profile picture's version, or <b>null</b> when there is no picture.</param>
public sealed record UserSettings(Guid UserId, string UserName, string TimeZoneId, string DefaultCurrency, uint? PictureVersion);
