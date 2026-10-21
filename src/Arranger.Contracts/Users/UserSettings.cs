using System;

namespace Faemiyah.Arranger.Contracts.Users;

/// <summary>
/// The signed-in user's own settings.
/// </summary>
/// <param name="UserId">The user ID.</param>
/// <param name="UserName">The handle.</param>
/// <param name="TimeZoneId">The IANA time zone times are shown in.</param>
/// <param name="DefaultCurrency">The ISO 4217 base currency for new arrangements.</param>
/// <param name="HasProfilePicture">Whether the user has uploaded a profile picture.</param>
public sealed record UserSettings(Guid UserId, string UserName, string TimeZoneId, string DefaultCurrency, bool HasProfilePicture);
