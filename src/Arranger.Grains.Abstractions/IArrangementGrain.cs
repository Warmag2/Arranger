using System;
using System.Threading.Tasks;
using Faemiyah.Arranger.Contracts.Arrangements;
using Orleans;

namespace Faemiyah.Arranger.Grains.Abstractions;

/// <summary>
/// One arrangement. All reads and writes of an arrangement go through its grain, one at a time.
/// </summary>
/// <remarks>
/// The grain key is the public arrangement ID. Every call takes the public ID of the acting user, which the caller
/// must have authenticated.
/// </remarks>
[Alias("Faemiyah.Arranger.IArrangementGrain")]
public interface IArrangementGrain : IGrainWithGuidKey
{
    /// <summary>
    /// Create the arrangement, with the user as its owner and first admin.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <param name="request">The arrangement details.</param>
    /// <returns>The new arrangement, or <see cref="CommandError.Conflict"/> if it already exists.</returns>
    [Alias("Create")]
    Task<CommandResult> CreateAsync(Guid userId, CreateArrangementRequest request);

    /// <summary>
    /// Get the current state.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <returns>The arrangement.</returns>
    [Alias("GetSnapshot")]
    Task<CommandResult> GetSnapshotAsync(Guid userId);

    /// <summary>
    /// Join the arrangement. Joining again is a no-op.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <returns>The arrangement.</returns>
    [Alias("Join")]
    Task<CommandResult> JoinAsync(Guid userId);

    /// <summary>
    /// Leave the arrangement, removing the member's votes. Refused for the owner and for anyone in an expense.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <returns>The arrangement.</returns>
    [Alias("Leave")]
    Task<CommandResult> LeaveAsync(Guid userId);

    /// <summary>
    /// Change the currency balances are settled in, clearing the exchange rates. The old base currency stays available.
    /// Admin only.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <param name="currency">The ISO 4217 currency.</param>
    /// <returns>The arrangement.</returns>
    [Alias("SetBaseCurrency")]
    Task<CommandResult> SetBaseCurrencyAsync(Guid userId, string currency);

    /// <summary>
    /// Add a currency for expenses (admin only), or change its exchange rate and conversion loss (any member). A
    /// currency added without a rate is seeded with the latest market rate when one is available. Requires the
    /// expenses to be enabled.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <param name="currency">The ISO 4217 currency, other than the base currency.</param>
    /// <param name="request">The rate, or <b>null</b> when not known, and the conversion loss.</param>
    /// <returns>The arrangement.</returns>
    [Alias("SetExchangeRate")]
    Task<CommandResult> SetExchangeRateAsync(Guid userId, string currency, SetExchangeRateRequest request);

    /// <summary>
    /// Remove a currency that no expense uses. Admin only.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <param name="currency">The ISO 4217 currency.</param>
    /// <returns>The arrangement.</returns>
    [Alias("RemoveCurrency")]
    Task<CommandResult> RemoveCurrencyAsync(Guid userId, string currency);

    /// <summary>
    /// Refresh every added currency's exchange rate from the latest market rates, keeping each conversion loss. Admin
    /// only.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <returns>The arrangement.</returns>
    [Alias("RefreshRates")]
    Task<CommandResult> RefreshRatesAsync(Guid userId);

    /// <summary>
    /// Open or close participation. Admin only.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <param name="isOpen">Whether new users may join.</param>
    /// <returns>The arrangement.</returns>
    [Alias("SetParticipationOpen")]
    Task<CommandResult> SetParticipationOpenAsync(Guid userId, bool isOpen);

    /// <summary>
    /// Open or close the arrangement for new time suggestions from non-admin members. When closed, only admins can
    /// add times. Admin only.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <param name="isAllowed">Whether non-admin members may suggest new times.</param>
    /// <returns>The arrangement.</returns>
    [Alias("SetAllowNewTimes")]
    Task<CommandResult> SetAllowNewTimesAsync(Guid userId, bool isAllowed);

    /// <summary>
    /// Enable or disable the schedule and expenses tabs. A disabled tab stays visible but refuses changes. Admin only.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <param name="request">Which tabs are enabled.</param>
    /// <returns>The arrangement.</returns>
    [Alias("SetTabs")]
    Task<CommandResult> SetTabsAsync(Guid userId, SetTabsRequest request);

    /// <summary>
    /// Grant or revoke admin rights. Owner only; the owner always stays admin.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <param name="memberUserId">The member whose rights change.</param>
    /// <param name="isAdmin">Whether the member is an admin.</param>
    /// <returns>The arrangement.</returns>
    [Alias("SetAdmin")]
    Task<CommandResult> SetAdminAsync(Guid userId, Guid memberUserId, bool isAdmin);

    /// <summary>
    /// Change the title and description. Admin only.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <param name="request">The new details.</param>
    /// <returns>The arrangement.</returns>
    [Alias("UpdateDetails")]
    Task<CommandResult> UpdateDetailsAsync(Guid userId, UpdateArrangementDetailsRequest request);

    /// <summary>
    /// Delete the arrangement and everything in it. Owner only.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <returns>A result without a snapshot.</returns>
    [Alias("Delete")]
    Task<CommandResult> DeleteAsync(Guid userId);

    /// <summary>
    /// Reload the cached profile of a member, after they changed it.
    /// </summary>
    /// <param name="memberUserId">The member.</param>
    /// <returns>A task.</returns>
    [Alias("RefreshMember")]
    Task RefreshMemberAsync(Guid memberUserId);

    /// <summary>
    /// Propose a time. Members only, while the poll is open.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <param name="request">The time.</param>
    /// <returns>The arrangement.</returns>
    [Alias("AddTimeOption")]
    Task<CommandResult> AddTimeOptionAsync(Guid userId, AddTimeOptionRequest request);

    /// <summary>
    /// Remove a proposed time and its votes. Members only, while the poll is open.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <param name="timeOptionId">The time option.</param>
    /// <returns>The arrangement.</returns>
    [Alias("RemoveTimeOption")]
    Task<CommandResult> RemoveTimeOptionAsync(Guid userId, long timeOptionId);

    /// <summary>
    /// Answer a proposed time, replacing an earlier answer. Members only, while the poll is open.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <param name="timeOptionId">The time option.</param>
    /// <param name="answer">The answer.</param>
    /// <returns>The arrangement.</returns>
    [Alias("SetVote")]
    Task<CommandResult> SetVoteAsync(Guid userId, long timeOptionId, VoteAnswer answer);

    /// <summary>
    /// Pick the final time, or clear the pick, while the schedule is enabled. Everyone sees the pick at once; disabling
    /// the schedule with <see cref="SetTabsAsync"/> confirms it. Admin only.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <param name="timeOptionId">The picked time option, or <b>null</b> to clear the pick.</param>
    /// <returns>The arrangement.</returns>
    [Alias("PickTime")]
    Task<CommandResult> PickTimeAsync(Guid userId, long? timeOptionId);

    /// <summary>
    /// Add an expense. Members only, while the expenses are enabled.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <param name="request">The expense.</param>
    /// <returns>The arrangement.</returns>
    [Alias("AddExpense")]
    Task<CommandResult> AddExpenseAsync(Guid userId, ExpenseRequest request);

    /// <summary>
    /// Replace any expense. Members only, while the expenses are enabled.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <param name="expenseId">The expense.</param>
    /// <param name="request">The new expense.</param>
    /// <returns>The arrangement.</returns>
    [Alias("UpdateExpense")]
    Task<CommandResult> UpdateExpenseAsync(Guid userId, long expenseId, ExpenseRequest request);

    /// <summary>
    /// Delete any expense. Members only, while the expenses are enabled.
    /// </summary>
    /// <param name="userId">The acting user.</param>
    /// <param name="expenseId">The expense.</param>
    /// <returns>The arrangement.</returns>
    [Alias("DeleteExpense")]
    Task<CommandResult> DeleteExpenseAsync(Guid userId, long expenseId);
}
