using System;

namespace Faemiyah.Arranger.Data.Queries;

/// <summary>
/// The public part of a user, as needed by arrangements.
/// </summary>
/// <param name="Id">The internal user ID.</param>
/// <param name="UniqueId">The public user ID.</param>
/// <param name="UserName">The username.</param>
/// <param name="DefaultCurrency">The ISO 4217 default currency.</param>
/// <param name="PictureVersion">The profile picture's row version, or <b>null</b> when there is no picture.</param>
public sealed record UserProfile(long Id, Guid UniqueId, string UserName, string DefaultCurrency, uint? PictureVersion);