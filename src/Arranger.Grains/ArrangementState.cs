using System;
using System.Collections.Generic;
using System.Linq;
using Faemiyah.Arranger.Contracts.Arrangements;
using Faemiyah.Arranger.Data.Entities;
using Faemiyah.Arranger.Data.Queries;
using Faemiyah.Arranger.Grains.Abstractions;

namespace Faemiyah.Arranger.Grains;

/// <summary>
/// The in-memory state of one arrangement activation: the entity graph, the member profiles, and the version.
/// </summary>
/// <remarks>
/// The <c>Check*</c> methods return the refusal, or <b>null</b> when the command may proceed.
/// </remarks>
public sealed class ArrangementState
{
    private readonly Dictionary<long, UserProfile> _profiles = [];
    private readonly Guid _epoch = Guid.NewGuid();
    private Arrangement? _arrangement;
    private long _sequence;

    /// <summary>
    /// Whether the arrangement exists.
    /// </summary>
    public bool Exists => _arrangement != null;

    /// <summary>
    /// The arrangement.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when it does not exist; check first.</exception>
    public Arrangement Arrangement => _arrangement ?? throw new InvalidOperationException("The arrangement is not loaded.");

    /// <summary>
    /// The internal IDs of every user whose profile the snapshot needs.
    /// </summary>
    /// <param name="arrangement">The loaded arrangement with its members.</param>
    /// <returns>The user IDs.</returns>
    public static IReadOnlyCollection<long> GetUserIds(Arrangement arrangement)
    {
        ArgumentNullException.ThrowIfNull(arrangement);
        return arrangement.Members.Select(m => m.UserId)
            .Append(arrangement.OwnerUserId)
            .Concat(arrangement.TimeOptions.Select(o => o.CreatedByUserId))
            .Concat(arrangement.TimeOptions.SelectMany(o => o.Votes).Select(v => v.UserId))
            .Concat(arrangement.Expenses.SelectMany(e => e.Participations.Select(p => p.UserId).Append(e.PaidByUserId)))
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Replace the whole state.
    /// </summary>
    /// <param name="arrangement">The arrangement with its members, or <b>null</b> when it does not exist.</param>
    /// <param name="profiles">The profiles of <see cref="GetUserIds"/>.</param>
    public void Load(Arrangement? arrangement, IEnumerable<UserProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        _arrangement = arrangement;
        _profiles.Clear();
        foreach (var profile in profiles)
        {
            _profiles[profile.Id] = profile;
        }
    }

    /// <summary>
    /// Forget the arrangement after it was deleted.
    /// </summary>
    public void Clear()
    {
        Load(null, []);
        _sequence++;
    }

    /// <summary>
    /// Replace the arrangement row, keeping the loaded members, time options, expenses and rates.
    /// </summary>
    /// <param name="updated">The saved arrangement row.</param>
    public void ReplaceArrangement(Arrangement updated)
    {
        ArgumentNullException.ThrowIfNull(updated);
        updated.Owner = null;
        updated.Members = Arrangement.Members;
        updated.TimeOptions = Arrangement.TimeOptions;
        updated.Expenses = Arrangement.Expenses;
        updated.ExchangeRates = Arrangement.ExchangeRates;
        _arrangement = updated;
    }

    /// <summary>
    /// Add or replace an exchange rate.
    /// </summary>
    /// <param name="rate">The saved rate row.</param>
    public void SetExchangeRate(ExchangeRate rate)
    {
        ArgumentNullException.ThrowIfNull(rate);
        rate.Arrangement = null;
        RemoveExchangeRate(rate.Currency);
        Arrangement.ExchangeRates.Add(rate);
    }

    /// <summary>
    /// Replace all exchange rates.
    /// </summary>
    /// <param name="rates">The saved rate rows.</param>
    public void ReplaceExchangeRates(IEnumerable<ExchangeRate> rates)
    {
        ArgumentNullException.ThrowIfNull(rates);
        Arrangement.ExchangeRates = rates.ToList();
        Arrangement.ExchangeRates.ForEach(r => r.Arrangement = null);
    }

    /// <summary>
    /// Remove an exchange rate.
    /// </summary>
    /// <param name="currency">The currency.</param>
    public void RemoveExchangeRate(string currency)
    {
        Arrangement.ExchangeRates.RemoveAll(r => r.Currency == currency);
    }

    /// <summary>
    /// Find an exchange rate.
    /// </summary>
    /// <param name="currency">The currency.</param>
    /// <returns>The rate, or <b>null</b> when the currency has none.</returns>
    public ExchangeRate? FindExchangeRate(string currency)
    {
        return _arrangement?.ExchangeRates.Find(r => r.Currency == currency);
    }

    /// <summary>
    /// Remove a member with their votes.
    /// </summary>
    /// <param name="member">The member row.</param>
    public void RemoveMember(ArrangementMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        Arrangement.Members.RemoveAll(m => m.Id == member.Id);
        foreach (var option in Arrangement.TimeOptions)
        {
            option.Votes.RemoveAll(v => v.UserId == member.UserId);
        }
    }

    /// <summary>
    /// Add or replace an expense.
    /// </summary>
    /// <param name="expense">The saved expense row with its participations.</param>
    public void SetExpense(Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);
        expense.Arrangement = null;
        expense.PaidBy = null;
        RemoveExpense(expense.Id);
        Arrangement.Expenses.Add(expense);
    }

    /// <summary>
    /// Remove an expense.
    /// </summary>
    /// <param name="expenseId">The expense ID.</param>
    public void RemoveExpense(long expenseId)
    {
        Arrangement.Expenses.RemoveAll(e => e.Id == expenseId);
    }

    /// <summary>
    /// Find an expense.
    /// </summary>
    /// <param name="expenseId">The expense ID.</param>
    /// <returns>The expense, or <b>null</b> when it is not in this arrangement.</returns>
    public Expense? FindExpense(long expenseId)
    {
        return _arrangement?.Expenses.Find(e => e.Id == expenseId);
    }

    /// <summary>
    /// Add a new time option.
    /// </summary>
    /// <param name="option">The saved time option row.</param>
    public void AddTimeOption(TimeOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        option.Arrangement = null;
        option.CreatedBy = null;
        option.Votes = [];
        Arrangement.TimeOptions.Add(option);
    }

    /// <summary>
    /// Remove a time option with its votes.
    /// </summary>
    /// <param name="timeOptionId">The time option ID.</param>
    public void RemoveTimeOption(long timeOptionId)
    {
        Arrangement.TimeOptions.RemoveAll(o => o.Id == timeOptionId);
    }

    /// <summary>
    /// Add or replace a vote.
    /// </summary>
    /// <param name="vote">The saved vote row.</param>
    public void SetVote(Vote vote)
    {
        ArgumentNullException.ThrowIfNull(vote);
        vote.TimeOption = null;
        vote.User = null;
        var votes = FindTimeOption(vote.TimeOptionId)!.Votes;
        votes.RemoveAll(v => v.UserId == vote.UserId);
        votes.Add(vote);
    }

    /// <summary>
    /// Find a time option.
    /// </summary>
    /// <param name="timeOptionId">The time option ID.</param>
    /// <returns>The time option, or <b>null</b> when it is not in this arrangement.</returns>
    public TimeOption? FindTimeOption(long timeOptionId)
    {
        return _arrangement?.TimeOptions.Find(o => o.Id == timeOptionId);
    }

    /// <summary>
    /// Add a new member.
    /// </summary>
    /// <param name="member">The saved member row.</param>
    /// <param name="profile">The member's profile.</param>
    public void AddMember(ArrangementMember member, UserProfile profile)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(profile);
        _profiles[profile.Id] = profile;
        Arrangement.Members.Add(member);
    }

    /// <summary>
    /// Replace a member row.
    /// </summary>
    /// <param name="updated">The saved member row.</param>
    public void ReplaceMember(ArrangementMember updated)
    {
        ArgumentNullException.ThrowIfNull(updated);
        updated.Arrangement = null;
        updated.User = null;
        var members = Arrangement.Members;
        members[members.FindIndex(m => m.Id == updated.Id)] = updated;
    }

    /// <summary>
    /// Replace a cached profile.
    /// </summary>
    /// <param name="profile">The new profile.</param>
    /// <returns><b>true</b> when it differed from the cached one.</returns>
    public bool ReplaceProfile(UserProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!_profiles.TryGetValue(profile.Id, out var cached) || cached == profile)
        {
            return false;
        }

        _profiles[profile.Id] = profile;
        return true;
    }

    /// <summary>
    /// Whether a user is cached, i.e. a member or the owner.
    /// </summary>
    /// <param name="userId">The public user ID.</param>
    /// <returns><b>true</b> when cached.</returns>
    public bool HasProfile(Guid userId)
    {
        return FindProfile(userId) != null;
    }

    /// <summary>
    /// Find a member.
    /// </summary>
    /// <param name="userId">The public user ID.</param>
    /// <returns>The member, or <b>null</b> when the user is not a member.</returns>
    public ArrangementMember? FindMember(Guid userId)
    {
        var profile = FindProfile(userId);
        return profile == null || _arrangement == null ? null : _arrangement.Members.Find(m => m.UserId == profile.Id);
    }

    /// <summary>
    /// Check that the arrangement exists.
    /// </summary>
    /// <returns>The refusal, or <b>null</b> when it exists.</returns>
    public CommandResult? CheckExists()
    {
        return Exists ? null : CommandResult.Failure(CommandError.NotFound, "The arrangement does not exist.");
    }

    /// <summary>
    /// Check that the user is a member.
    /// </summary>
    /// <param name="userId">The public user ID.</param>
    /// <returns>The refusal, or <b>null</b> when allowed.</returns>
    public CommandResult? CheckMember(Guid userId)
    {
        if (CheckExists() is { } missing)
        {
            return missing;
        }

        return FindMember(userId) == null ? CommandResult.Failure(CommandError.NotMember, "Join the arrangement first.") : null;
    }

    /// <summary>
    /// Check that the user is a member and the schedule is enabled.
    /// </summary>
    /// <param name="userId">The public user ID.</param>
    /// <returns>The refusal, or <b>null</b> when allowed.</returns>
    public CommandResult? CheckScheduleEnabled(Guid userId)
    {
        if (CheckMember(userId) is { } denied)
        {
            return denied;
        }

        return Arrangement.IsScheduleDisabled ? CommandResult.Failure(CommandError.Forbidden, "The schedule is disabled.") : null;
    }

    /// <summary>
    /// Check that the user is a member and the expenses are enabled.
    /// </summary>
    /// <param name="userId">The public user ID.</param>
    /// <returns>The refusal, or <b>null</b> when allowed.</returns>
    public CommandResult? CheckExpensesEnabled(Guid userId)
    {
        if (CheckMember(userId) is { } denied)
        {
            return denied;
        }

        return Arrangement.IsExpensesDisabled ? CommandResult.Failure(CommandError.Forbidden, "The expenses are disabled.") : null;
    }

    /// <summary>
    /// Check that the user is an admin member.
    /// </summary>
    /// <param name="userId">The public user ID.</param>
    /// <returns>The refusal, or <b>null</b> when allowed.</returns>
    public CommandResult? CheckAdmin(Guid userId)
    {
        if (CheckMember(userId) is { } denied)
        {
            return denied;
        }

        return FindMember(userId)!.IsAdmin ? null : CommandResult.Failure(CommandError.Forbidden, "Only admins can do this.");
    }

    /// <summary>
    /// Check that the user is the owner.
    /// </summary>
    /// <param name="userId">The public user ID.</param>
    /// <returns>The refusal, or <b>null</b> when allowed.</returns>
    public CommandResult? CheckOwner(Guid userId)
    {
        if (CheckMember(userId) is { } denied)
        {
            return denied;
        }

        return FindMember(userId)!.UserId == Arrangement.OwnerUserId ? null : CommandResult.Failure(CommandError.Forbidden, "Only the owner can do this.");
    }

    /// <summary>
    /// Record a change.
    /// </summary>
    /// <returns>A successful result with the new snapshot.</returns>
    public CommandResult Changed()
    {
        _sequence++;
        return Current();
    }

    /// <summary>
    /// The current state, without recording a change.
    /// </summary>
    /// <returns>A successful result with the snapshot, or <see cref="CommandError.NotFound"/>.</returns>
    public CommandResult Current()
    {
        return CheckExists() ?? CommandResult.Success(ToSnapshot());
    }

    /// <summary>
    /// Build the snapshot sent to clients.
    /// </summary>
    /// <returns>The snapshot.</returns>
    public ArrangementSnapshot ToSnapshot()
    {
        var arrangement = Arrangement;
        var members = arrangement.Members
            .OrderBy(m => m.JoinedAt)
            .ThenBy(m => m.Id)
            .Select(m =>
            {
                var profile = _profiles[m.UserId];
                return new ArrangementMemberSnapshot(profile.UniqueId, profile.UserName, m.IsAdmin, m.JoinedAt);
            })
            .ToList();

        var timeOptions = arrangement.TimeOptions
            .OrderBy(o => o.DateBegin)
            .ThenBy(o => o.DateEnd)
            .ThenBy(o => o.Id)
            .Select(o => new TimeOptionSnapshot(
                o.Id,
                o.DateBegin!.Value,
                o.DateEnd,
                o.IsWholeDay,
                _profiles[o.CreatedByUserId].UniqueId,
                o.Votes.OrderBy(v => v.Id).Select(v => new VoteSnapshot(_profiles[v.UserId].UniqueId, v.Answer)).ToList()))
            .ToList();

        var expenses = arrangement.Expenses
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Id)
            .Select(e => new ExpenseSnapshot(
                e.Id,
                e.Description,
                e.Amount,
                e.Currency,
                _profiles[e.PaidByUserId].UniqueId,
                e.Participations.Select(p => _profiles[p.UserId].UniqueId).Order().ToList(),
                e.Date,
                e.IsSettlementPayment))
            .ToList();

        return new ArrangementSnapshot(
            arrangement.UniqueId,
            arrangement.Title,
            arrangement.Description,
            _profiles[arrangement.OwnerUserId].UniqueId,
            arrangement.CreatedAt,
            arrangement.AllowNewTimes,
            arrangement.AllowNewUsers,
            arrangement.BaseCurrency,
            arrangement.FinalTimeOptionId,
            arrangement.IsScheduleDisabled,
            arrangement.IsExpensesDisabled,
            members,
            timeOptions,
            expenses,
            arrangement.ExchangeRates.ToDictionary(r => r.Currency, r => new ExchangeRateSnapshot(r.Rate, r.LossPercent), StringComparer.Ordinal),
            new ArrangementVersion(_epoch, _sequence));
    }

    private UserProfile? FindProfile(Guid userId)
    {
        return _profiles.Values.FirstOrDefault(p => p.UniqueId == userId);
    }
}
