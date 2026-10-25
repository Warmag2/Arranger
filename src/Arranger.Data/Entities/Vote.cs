using Faemiyah.Arranger.Contracts.Arrangements;
using Microsoft.EntityFrameworkCore;
using Faemiyah.Repository.Entities;

namespace Faemiyah.Arranger.Data.Entities;

/// <summary>
/// A member's answer to a time option.
/// </summary>
[Index(nameof(TimeOptionId), nameof(UserId), IsUnique = true)]
public class Vote : EntityBase<long>
{
    /// <summary>
    /// The time option's ID.
    /// </summary>
    public long TimeOptionId { get; set; }

    /// <summary>
    /// The time option.
    /// </summary>
    public TimeOption? TimeOption { get; set; }

    /// <summary>
    /// The member's user ID.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// The member.
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// The answer.
    /// </summary>
    public VoteAnswer Answer { get; set; }

    /// <summary>
    /// Copy the vote's own columns, for an update that must not touch this instance.
    /// </summary>
    /// <returns>The copy.</returns>
    public Vote CloneDetails()
    {
        var clone = (Vote)MemberwiseClone();
        clone.TimeOption = null;
        clone.User = null;
        return clone;
    }
}
