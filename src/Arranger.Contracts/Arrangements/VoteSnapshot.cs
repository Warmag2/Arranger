using System;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// A member's answer to a time option.
/// </summary>
/// <param name="UserId">The member's user ID.</param>
/// <param name="Answer">The answer.</param>
public sealed record VoteSnapshot(Guid UserId, VoteAnswer Answer);
