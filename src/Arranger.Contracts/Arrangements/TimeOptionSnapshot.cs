using System;
using System.Collections.Generic;
using System.Linq;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// A proposed time in the schedule poll.
/// </summary>
/// <param name="Id">The option ID.</param>
/// <param name="DateBegin">The start in UTC. For a whole-day option, the UTC midnight of the day.</param>
/// <param name="DateEnd">The end in UTC, or <b>null</b> when open-ended or whole-day.</param>
/// <param name="IsWholeDay">Whether the option is the UTC date of <paramref name="DateBegin"/>, without a time.</param>
/// <param name="CreatedByUserId">The user ID of the member who proposed it.</param>
/// <param name="Votes">The answers, one per member at most.</param>
public sealed record TimeOptionSnapshot(
    long Id,
    DateTime DateBegin,
    DateTime? DateEnd,
    bool IsWholeDay,
    Guid CreatedByUserId,
    IReadOnlyList<VoteSnapshot> Votes)
{
    /// <summary>
    /// Find a member's answer.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>The answer, or <b>null</b> when the member has not answered.</returns>
    public VoteAnswer? FindAnswer(Guid userId)
    {
        return Votes.FirstOrDefault(v => v.UserId == userId)?.Answer;
    }

    /// <summary>
    /// Count the members who gave an answer.
    /// </summary>
    /// <param name="answer">The answer.</param>
    /// <returns>The count.</returns>
    public int Count(VoteAnswer answer)
    {
        return Votes.Count(v => v.Answer == answer);
    }
}
