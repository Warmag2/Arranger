using System;
using System.Collections.Generic;
using System.Linq;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// The complete state of an arrangement, as seen by every viewer.
/// </summary>
/// <param name="Id">The arrangement ID, which is also the invite link.</param>
/// <param name="Title">The title.</param>
/// <param name="Description">The description, possibly empty.</param>
/// <param name="OwnerUserId">The creator's user ID.</param>
/// <param name="CreatedAt">The creation time in UTC.</param>
/// <param name="IsParticipationClosed">Whether new members are no longer accepted.</param>
/// <param name="BaseCurrency">The ISO 4217 currency balances are settled in.</param>
/// <param name="FinalTimeOptionId">The picked time option. Closing the schedule confirms it.</param>
/// <param name="IsScheduleDisabled">Whether the schedule tab is disabled, i.e. read-only.</param>
/// <param name="IsExpensesDisabled">Whether the expenses tab is disabled, i.e. read-only.</param>
/// <param name="Members">The members, in joining order.</param>
/// <param name="TimeOptions">The proposed times, in chronological order.</param>
/// <param name="Expenses">The expenses, by date and then in the order they were added.</param>
/// <param name="ExchangeRates">The other currencies the arrangement uses, with their rates to <paramref name="BaseCurrency"/>.</param>
/// <param name="Version">The version of this snapshot.</param>
public sealed record ArrangementSnapshot(
    Guid Id,
    string Title,
    string Description,
    Guid OwnerUserId,
    DateTime CreatedAt,
    bool IsParticipationClosed,
    string BaseCurrency,
    long? FinalTimeOptionId,
    bool IsScheduleDisabled,
    bool IsExpensesDisabled,
    IReadOnlyList<ArrangementMemberSnapshot> Members,
    IReadOnlyList<TimeOptionSnapshot> TimeOptions,
    IReadOnlyList<ExpenseSnapshot> Expenses,
    IReadOnlyDictionary<string, ExchangeRateSnapshot> ExchangeRates,
    ArrangementVersion Version)
{
    /// <summary>
    /// Find a member.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>The member, or <b>null</b> when the user is not a member.</returns>
    public ArrangementMemberSnapshot? FindMember(Guid userId)
    {
        return Members.FirstOrDefault(m => m.UserId == userId);
    }

    /// <summary>
    /// The currencies expenses can be recorded in: the base currency first, then the others in order.
    /// </summary>
    /// <returns>The ISO 4217 codes.</returns>
    public IReadOnlyList<string> AvailableCurrencies()
    {
        return [BaseCurrency, .. ExchangeRates.Keys.Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The tab to open when the link names none: the expenses when only they are enabled, otherwise the schedule.
    /// </summary>
    /// <returns>The tab.</returns>
    public ArrangementTab DefaultTab()
    {
        return IsScheduleDisabled && !IsExpensesDisabled ? ArrangementTab.Expenses : ArrangementTab.Schedule;
    }
}
