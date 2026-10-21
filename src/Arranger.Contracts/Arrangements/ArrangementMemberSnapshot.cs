using System;
using System.Collections.Generic;
using System.Linq;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// A member of an arrangement.
/// </summary>
/// <param name="UserId">The user ID.</param>
/// <param name="UserName">The user's handle.</param>
/// <param name="IsAdmin">Whether the member may manage the arrangement. Always <b>true</b> for the owner.</param>
/// <param name="JoinedAt">The joining time in UTC.</param>
public sealed record ArrangementMemberSnapshot(Guid UserId, string UserName, bool IsAdmin, DateTime JoinedAt);
