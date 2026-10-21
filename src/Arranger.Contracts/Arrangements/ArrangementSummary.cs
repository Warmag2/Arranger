using System;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// One arrangement in the list of the user's arrangements.
/// </summary>
/// <param name="Id">The arrangement ID.</param>
/// <param name="Title">The title.</param>
/// <param name="CreatedAt">The creation time in UTC.</param>
/// <param name="MemberCount">The number of members.</param>
/// <param name="IsOwner">Whether the user created the arrangement.</param>
/// <param name="IsAdmin">Whether the user is an admin.</param>
public sealed record ArrangementSummary(Guid Id, string Title, DateTime CreatedAt, int MemberCount, bool IsOwner, bool IsAdmin);
