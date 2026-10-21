using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RepositoryPrototype.Entities;

namespace Faemiyah.Arranger.Data.Entities;

/// <summary>
/// An expense in an arrangement, split equally between its participants.
/// </summary>
public class Expense : EntityBase<long>
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
    /// The description.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The amount, positive, in <see cref="Currency"/>.
    /// </summary>
    [Column(TypeName = "numeric(19,4)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// The ISO 4217 currency.
    /// </summary>
    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// The user ID of the member who paid.
    /// </summary>
    public long PaidByUserId { get; set; }

    /// <summary>
    /// The member who paid.
    /// </summary>
    public User? PaidBy { get; set; }

    /// <summary>
    /// The date it was paid.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Whether it is a settling payment between members. Only affects visuals.
    /// </summary>
    public bool IsSettlementPayment { get; set; }

    /// <summary>
    /// The members who share it.
    /// </summary>
    public List<ExpenseParticipation> Participations { get; set; } = [];

    /// <summary>
    /// Copy the expense's own columns, without the participations, for an update that must not touch this instance.
    /// </summary>
    /// <returns>The copy.</returns>
    public Expense CloneDetails()
    {
        var clone = (Expense)MemberwiseClone();
        clone.Arrangement = null;
        clone.PaidBy = null;
        clone.Participations = [];
        return clone;
    }
}
