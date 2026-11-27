using System;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// A member of an arrangement.
/// </summary>
/// <param name="UserId">The user ID.</param>
/// <param name="UserName">The user's username.</param>
/// <param name="IsAdmin">Whether the member may manage the arrangement. Always <b>true</b> for the owner.</param>
/// <param name="JoinedAt">The joining time in UTC.</param>
/// <param name="PictureVersion">The profile picture's version, or <b>null</b> when there is no picture.</param>
public sealed record ArrangementMemberSnapshot(Guid UserId, string UserName, bool IsAdmin, DateTime JoinedAt, uint? PictureVersion);
