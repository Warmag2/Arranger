using Microsoft.EntityFrameworkCore;
using Faemiyah.Repository.Entities;

namespace Faemiyah.Arranger.Data.Entities;

/// <summary>
/// A member's share in an expense.
/// </summary>
[Index(nameof(ExpenseId), nameof(UserId), IsUnique = true)]
public class ExpenseParticipation : EntityBase<long>
{
    /// <summary>
    /// The expense's ID.
    /// </summary>
    public long ExpenseId { get; set; }

    /// <summary>
    /// The expense.
    /// </summary>
    public Expense? Expense { get; set; }

    /// <summary>
    /// The member's user ID.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// The member.
    /// </summary>
    public User? User { get; set; }
}
