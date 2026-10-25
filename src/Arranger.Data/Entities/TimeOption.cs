using System.Collections.Generic;
using Faemiyah.Repository.Entities;

namespace Faemiyah.Arranger.Data.Entities;

/// <summary>
/// A proposed time in an arrangement's schedule poll.
/// </summary>
/// <remarks>
/// <see cref="TemporalEntityBase{TKey}.DateBegin"/> is always set. A whole-day option starts at the UTC midnight of
/// the day and has no <see cref="TemporalEntityBase{TKey}.DateEnd"/>; <see cref="IsWholeDay"/> tells it apart from a
/// timed option without an end.
/// </remarks>
public class TimeOption : TemporalEntityBase<long>
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
    /// Whether the option is a whole UTC date rather than a time.
    /// </summary>
    public bool IsWholeDay { get; set; }

    /// <summary>
    /// The user ID of the member who proposed it.
    /// </summary>
    public long CreatedByUserId { get; set; }

    /// <summary>
    /// The member who proposed it.
    /// </summary>
    public User? CreatedBy { get; set; }

    /// <summary>
    /// The answers.
    /// </summary>
    public List<Vote> Votes { get; set; } = [];
}
