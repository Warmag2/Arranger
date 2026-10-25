using System;
using Microsoft.EntityFrameworkCore;
using Faemiyah.Repository.Entities;

namespace Faemiyah.Arranger.Data.Entities;

/// <summary>
/// A user's membership in an arrangement.
/// </summary>
[Index(nameof(ArrangementId), nameof(UserId), IsUnique = true)]
[Index(nameof(UserId))]
public class ArrangementMember : EntityBase<long>
{
    /// <summary>
    /// The arrangement's ID.
    /// </summary>
    public long ArrangementId { get; set; }

    /// <summary>
    /// The arrangement.
    /// </summary>
    public Arrangement? Arrangement { get; set; }

    /// <summary>
    /// The member's user ID.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// The member.
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// The joining time in UTC.
    /// </summary>
    public DateTime JoinedAt { get; set; }

    /// <summary>
    /// Whether the member may manage the arrangement. Always <b>true</b> for the owner.
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Copy the membership's own columns, for an update that must not touch this instance.
    /// </summary>
    /// <returns>The copy.</returns>
    public ArrangementMember CloneDetails()
    {
        var clone = (ArrangementMember)MemberwiseClone();
        clone.Arrangement = null;
        clone.User = null;
        return clone;
    }
}
