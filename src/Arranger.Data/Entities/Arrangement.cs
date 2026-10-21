using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using RepositoryPrototype.Entities;

namespace Faemiyah.Arranger.Data.Entities;

/// <summary>
/// An arrangement: a scheduling poll and a shared expense list for one group.
/// </summary>
/// <remarks>
/// <see cref="EntityBaseWithUuid{TKey}.UniqueId"/> is the arrangement ID used in links and grain keys.
/// </remarks>
public class Arrangement : EntityBaseWithUuid<long>
{
    /// <summary>
    /// The maximum length of <see cref="Title"/>.
    /// </summary>
    public const int TitleMaxLength = 200;

    /// <summary>
    /// The maximum length of <see cref="Description"/>.
    /// </summary>
    public const int DescriptionMaxLength = 2000;

    /// <summary>
    /// The title.
    /// </summary>
    [Required]
    [MaxLength(TitleMaxLength)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The description, possibly empty.
    /// </summary>
    [Required]
    [MaxLength(DescriptionMaxLength)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The creator's user ID.
    /// </summary>
    public long OwnerUserId { get; set; }

    /// <summary>
    /// The creator.
    /// </summary>
    public User? Owner { get; set; }

    /// <summary>
    /// The creation time in UTC.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Whether new members are no longer accepted.
    /// </summary>
    public bool IsParticipationClosed { get; set; }

    /// <summary>
    /// The ISO 4217 currency balances are settled in.
    /// </summary>
    [Required]
    [MaxLength(3)]
    public string BaseCurrency { get; set; } = "EUR";

    /// <summary>
    /// The picked time option. Closing the schedule confirms it.
    /// </summary>
    public long? FinalTimeOptionId { get; set; }

    /// <summary>
    /// Whether the schedule tab is disabled, i.e. read-only, which closes the poll.
    /// </summary>
    public bool IsScheduleDisabled { get; set; }

    /// <summary>
    /// Whether the expenses tab is disabled, i.e. read-only.
    /// </summary>
    public bool IsExpensesDisabled { get; set; }

    /// <summary>
    /// The members, including the owner.
    /// </summary>
    public List<ArrangementMember> Members { get; set; } = [];

    /// <summary>
    /// The proposed times.
    /// </summary>
    public List<TimeOption> TimeOptions { get; set; } = [];

    /// <summary>
    /// The expenses.
    /// </summary>
    public List<Expense> Expenses { get; set; } = [];

    /// <summary>
    /// The exchange rates to <see cref="BaseCurrency"/>.
    /// </summary>
    public List<ExchangeRate> ExchangeRates { get; set; } = [];

    /// <summary>
    /// Copy the arrangement's own columns, without the related rows, for an update that must not touch this instance.
    /// </summary>
    /// <returns>The copy.</returns>
    public Arrangement CloneDetails()
    {
        var clone = (Arrangement)MemberwiseClone();
        clone.Owner = null;
        clone.Members = [];
        clone.TimeOptions = [];
        clone.Expenses = [];
        clone.ExchangeRates = [];
        return clone;
    }
}
