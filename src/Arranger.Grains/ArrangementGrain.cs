using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Arranger.Contracts;
using Faemiyah.Arranger.Contracts.Arrangements;
using Faemiyah.Arranger.Data.Entities;
using Faemiyah.Arranger.Data.Queries;
using Faemiyah.Arranger.Data.Repositories;
using Faemiyah.Arranger.Grains.Abstractions;
using Microsoft.Extensions.Logging;
using Orleans;
using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Models;

namespace Faemiyah.Arranger.Grains;

/// <summary>
/// One arrangement, kept in memory while active and written through to the database on every change.
/// </summary>
/// <remarks>
/// Refusals are returned as failed results before any state changes. Every change is written to the database before
/// memory, so an exception leaves memory intact. A write the database refuses means this activation is stale, so it
/// is dropped. Every change is then pushed to the clients watching the arrangement; calls are not reentrant, so the
/// pushes leave in the order the changes happened.
/// </remarks>
public sealed class ArrangementGrain : Grain, IArrangementGrain
{
    private readonly ILogger<ArrangementGrain> _logger;
    private readonly ArrangementRepository _arrangements;
    private readonly ArrangementMemberRepository _members;
    private readonly IEntityRepository<long, TimeOption> _timeOptions;
    private readonly IEntityRepository<long, Vote> _votes;
    private readonly ExpenseRepository _expenses;
    private readonly IEntityRepository<long, ExchangeRate> _rates;
    private readonly UserQueries _userQueries;
    private readonly IArrangementNotifier _notifier;
    private readonly ICurrencyRateProvider _rateProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ArrangementState _state = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrangementGrain"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="arrangements">The arrangement repository.</param>
    /// <param name="members">The member repository.</param>
    /// <param name="timeOptions">The time option repository.</param>
    /// <param name="votes">The vote repository.</param>
    /// <param name="expenses">The expense repository.</param>
    /// <param name="rates">The exchange rate repository.</param>
    /// <param name="userQueries">The user queries.</param>
    /// <param name="notifier">Tells clients about changes.</param>
    /// <param name="rateProvider">The market exchange rate cache.</param>
    /// <param name="timeProvider">The time provider.</param>
    public ArrangementGrain(
        ILogger<ArrangementGrain> logger,
        ArrangementRepository arrangements,
        ArrangementMemberRepository members,
        IEntityRepository<long, TimeOption> timeOptions,
        IEntityRepository<long, Vote> votes,
        ExpenseRepository expenses,
        IEntityRepository<long, ExchangeRate> rates,
        UserQueries userQueries,
        IArrangementNotifier notifier,
        ICurrencyRateProvider rateProvider,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _arrangements = arrangements;
        _members = members;
        _timeOptions = timeOptions;
        _votes = votes;
        _expenses = expenses;
        _rates = rates;
        _userQueries = userQueries;
        _notifier = notifier;
        _rateProvider = rateProvider;
        _timeProvider = timeProvider;
    }

    private Guid ArrangementId => this.GetPrimaryKey();

    /// <inheritdoc />
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var id = ArrangementId;
        var arrangement = await _arrangements.GetAsync(a => a.UniqueId == id, processIncludes: true, cancellationToken: cancellationToken);
        var profiles = arrangement == null ? [] : await _userQueries.GetProfilesAsync(ArrangementState.GetUserIds(arrangement), cancellationToken);
        _state.Load(arrangement, profiles);
        await base.OnActivateAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CommandResult> CreateAsync(Guid userId, CreateArrangementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_state.Exists)
        {
            return CommandResult.Failure(CommandError.Conflict, "The arrangement already exists.");
        }

        var title = ArrangementRules.NormalizeText(request.Title);
        var description = ArrangementRules.NormalizeText(request.Description);
        if (ArrangementRules.ValidateDetails(title, description) is { } invalid)
        {
            return CommandResult.Failure(CommandError.Invalid, invalid);
        }

        if (await _userQueries.GetProfileAsync(userId) is not { } creator)
        {
            return UserNotFound();
        }

        var now = Now();
        var id = ArrangementId;
        var arrangement = new Arrangement
        {
            UniqueId = id,
            Title = title,
            Description = description,
            OwnerUserId = creator.Id,
            CreatedAt = now,
            BaseCurrency = creator.DefaultCurrency,
            AllowNewUsers = true,
            AllowNewTimes = false,
            IsScheduleDisabled = !request.IsScheduleEnabled,
            IsExpensesDisabled = !request.IsExpensesEnabled,
            Members = [new ArrangementMember { UserId = creator.Id, JoinedAt = now, IsAdmin = true }],
        };

        var added = await _arrangements.AddAsync(arrangement, a => a.UniqueId == id);
        if (!added.IsSuccess)
        {
            return WriteFailed(added.Status);
        }

        _state.Load(added.Value, [creator]);
        _logger.LogInformation("User {UserId} created arrangement {ArrangementId}.", userId, id);
        return await PublishAsync();
    }

    /// <inheritdoc />
    public Task<CommandResult> GetSnapshotAsync(Guid userId)
    {
        return Task.FromResult(_state.Current());
    }

    /// <inheritdoc />
    public async Task<CommandResult> JoinAsync(Guid userId)
    {
        if (_state.CheckExists() is { } missing)
        {
            return missing;
        }

        var arrangement = _state.Arrangement;
        if (_state.FindMember(userId) != null)
        {
            return _state.Current();
        }

        if (!arrangement.AllowNewUsers)
        {
            return CommandResult.Failure(CommandError.Forbidden, "Participation is closed.");
        }

        if (arrangement.Members.Count >= ArrangementRules.MaxMembers)
        {
            return CommandResult.Failure(CommandError.Invalid, $"An arrangement can have at most {ArrangementRules.MaxMembers} members.");
        }

        if (await _userQueries.GetProfileAsync(userId) is not { } profile)
        {
            return UserNotFound();
        }

        var member = new ArrangementMember { ArrangementId = arrangement.Id, UserId = profile.Id, JoinedAt = Now() };
        var added = await _members.AddAsync(member, m => m.ArrangementId == member.ArrangementId && m.UserId == member.UserId);
        if (!added.IsSuccess)
        {
            return WriteFailed(added.Status);
        }

        _state.AddMember(added.Value, profile);
        _logger.LogInformation("User {UserId} joined arrangement {ArrangementId}.", userId, ArrangementId);
        return await PublishAsync();
    }

    /// <inheritdoc />
    public async Task<CommandResult> LeaveAsync(Guid userId)
    {
        if (_state.CheckMember(userId) is { } denied)
        {
            return denied;
        }

        var member = _state.FindMember(userId)!;
        if (member.UserId == _state.Arrangement.OwnerUserId)
        {
            return CommandResult.Failure(CommandError.Invalid, "The owner cannot leave. Delete the arrangement instead.");
        }

        if (_state.Arrangement.Expenses.Exists(e => e.PaidByUserId == member.UserId || e.Participations.Exists(p => p.UserId == member.UserId)))
        {
            return CommandResult.Failure(CommandError.Invalid, "You cannot leave while you are part of an expense.");
        }

        var deleted = await _members.DeleteWithVotesAsync(member);
        if (!deleted.IsSuccess)
        {
            return WriteFailed(deleted.Status);
        }

        _state.RemoveMember(member);
        _logger.LogInformation("User {UserId} left arrangement {ArrangementId}.", userId, ArrangementId);
        return await PublishAsync();
    }

    /// <inheritdoc />
    public async Task<CommandResult> SetParticipationOpenAsync(Guid userId, bool isOpen)
    {
        if (_state.CheckAdmin(userId) is { } denied)
        {
            return denied;
        }

        if (_state.Arrangement.AllowNewUsers == isOpen)
        {
            return _state.Current();
        }

        var update = _state.Arrangement.CloneDetails();
        update.AllowNewUsers = isOpen;
        return await SaveArrangementAsync(update);
    }

    /// <inheritdoc />
    public async Task<CommandResult> SetAllowNewTimesAsync(Guid userId, bool isAllowed)
    {
        if (_state.CheckAdmin(userId) is { } denied)
        {
            return denied;
        }

        if (_state.Arrangement.AllowNewTimes == isAllowed)
        {
            return _state.Current();
        }

        var update = _state.Arrangement.CloneDetails();
        update.AllowNewTimes = isAllowed;
        return await SaveArrangementAsync(update);
    }

    /// <inheritdoc />
    public async Task<CommandResult> SetTabsAsync(Guid userId, SetTabsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_state.CheckAdmin(userId) is { } denied)
        {
            return denied;
        }

        var update = _state.Arrangement.CloneDetails();
        if (update.IsScheduleDisabled != request.IsScheduleEnabled && update.IsExpensesDisabled != request.IsExpensesEnabled)
        {
            return _state.Current();
        }

        update.IsScheduleDisabled = !request.IsScheduleEnabled;
        update.IsExpensesDisabled = !request.IsExpensesEnabled;
        return await SaveArrangementAsync(update);
    }

    /// <inheritdoc />
    public async Task<CommandResult> SetAdminAsync(Guid userId, Guid memberUserId, bool isAdmin)
    {
        if (_state.CheckOwner(userId) is { } denied)
        {
            return denied;
        }

        if (_state.FindMember(memberUserId) is not { } member)
        {
            return CommandResult.Failure(CommandError.NotFound, "The user is not a member.");
        }

        if (member.UserId == _state.Arrangement.OwnerUserId)
        {
            return CommandResult.Failure(CommandError.Invalid, "The owner is always an admin.");
        }

        if (member.IsAdmin == isAdmin)
        {
            return _state.Current();
        }

        var update = member.CloneDetails();
        update.IsAdmin = isAdmin;
        var saved = await _members.UpdateAsync(update, m => m.Id == update.Id);
        if (!saved.IsSuccess)
        {
            return WriteFailed(saved.Status);
        }

        _state.ReplaceMember(saved.Value);
        return await PublishAsync();
    }

    /// <inheritdoc />
    public async Task<CommandResult> UpdateDetailsAsync(Guid userId, UpdateArrangementDetailsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_state.CheckAdmin(userId) is { } denied)
        {
            return denied;
        }

        var title = ArrangementRules.NormalizeText(request.Title);
        var description = ArrangementRules.NormalizeText(request.Description);
        if (ArrangementRules.ValidateDetails(title, description) is { } invalid)
        {
            return CommandResult.Failure(CommandError.Invalid, invalid);
        }

        var update = _state.Arrangement.CloneDetails();
        if (update.Title == title && update.Description == description)
        {
            return _state.Current();
        }

        update.Title = title;
        update.Description = description;
        return await SaveArrangementAsync(update);
    }

    /// <inheritdoc />
    public async Task<CommandResult> DeleteAsync(Guid userId)
    {
        if (_state.CheckOwner(userId) is { } denied)
        {
            return denied;
        }

        // The database cascades the delete to everything in the arrangement.
        var deleted = await _arrangements.DeleteAsync(ArrangementId);
        if (!deleted.IsSuccess)
        {
            return WriteFailed(deleted.Status);
        }

        _state.Clear();
        DeactivateOnIdle();
        await _notifier.DeletedAsync(ArrangementId);
        _logger.LogInformation("User {UserId} deleted arrangement {ArrangementId}.", userId, ArrangementId);
        return CommandResult.Success(null);
    }

    /// <inheritdoc />
    public async Task RefreshMemberAsync(Guid memberUserId)
    {
        if (!_state.Exists || !_state.HasProfile(memberUserId))
        {
            return;
        }

        var profile = await _userQueries.GetProfileAsync(memberUserId);
        if (profile != null && _state.ReplaceProfile(profile))
        {
            await PublishAsync();
        }
    }

    /// <inheritdoc />
    public async Task<CommandResult> AddTimeOptionAsync(Guid userId, AddTimeOptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_state.CheckScheduleEnabled(userId) is { } denied)
        {
            return denied;
        }

        if (!_state.Arrangement.AllowNewTimes && !_state.FindMember(userId)!.IsAdmin)
        {
            return CommandResult.Failure(CommandError.Forbidden, "Only admins can add new times.");
        }

        var begin = ArrangementRules.TruncateToMinute(request.DateBegin);
        var end = request.DateEnd is { } value ? ArrangementRules.TruncateToMinute(value) : (DateTime?)null;
        if (ArrangementRules.ValidateTimeOption(begin, end, request.IsWholeDay) is { } invalid)
        {
            return CommandResult.Failure(CommandError.Invalid, invalid);
        }

        var options = _state.Arrangement.TimeOptions;
        if (options.Count >= ArrangementRules.MaxTimeOptions)
        {
            return CommandResult.Failure(CommandError.Invalid, $"An arrangement can have at most {ArrangementRules.MaxTimeOptions} time options.");
        }

        if (options.Exists(o => o.DateBegin == begin && o.DateEnd == end && o.IsWholeDay == request.IsWholeDay))
        {
            return CommandResult.Failure(CommandError.Invalid, "That time is already an option.");
        }

        var option = new TimeOption
        {
            ArrangementId = _state.Arrangement.Id,
            DateBegin = begin,
            DateEnd = end,
            IsWholeDay = request.IsWholeDay,
            CreatedByUserId = _state.FindMember(userId)!.UserId,
        };

        var added = await _timeOptions.AddAsync(option);
        if (!added.IsSuccess)
        {
            return WriteFailed(added.Status);
        }

        _state.AddTimeOption(added.Value);
        return await PublishAsync();
    }

    /// <inheritdoc />
    public async Task<CommandResult> RemoveTimeOptionAsync(Guid userId, long timeOptionId)
    {
        if (_state.CheckScheduleEnabled(userId) is { } denied)
        {
            return denied;
        }

        if (!_state.Arrangement.AllowNewTimes && !_state.FindMember(userId)!.IsAdmin)
        {
            return CommandResult.Failure(CommandError.Forbidden, "Only admins can remove times.");
        }

        if (_state.FindTimeOption(timeOptionId) == null)
        {
            return TimeOptionNotFound();
        }

        // The database cascades the delete to the votes.
        var deleted = await _timeOptions.DeleteAsync(o => o.Id == timeOptionId);
        if (!deleted.IsSuccess || deleted.Value.Count == 0)
        {
            return WriteFailed(deleted.IsSuccess ? OperationStatus.NotFound : deleted.Status);
        }

        _state.RemoveTimeOption(timeOptionId);
        return await PublishAsync();
    }

    /// <inheritdoc />
    public async Task<CommandResult> SetVoteAsync(Guid userId, long timeOptionId, VoteAnswer answer)
    {
        if (_state.CheckScheduleEnabled(userId) is { } denied)
        {
            return denied;
        }

        if (!Enum.IsDefined(answer))
        {
            return CommandResult.Failure(CommandError.Invalid, "The answer must be yes, maybe or no.");
        }

        if (_state.FindTimeOption(timeOptionId) is not { } option)
        {
            return TimeOptionNotFound();
        }

        var memberId = _state.FindMember(userId)!.UserId;
        var existing = option.Votes.Find(v => v.UserId == memberId);
        if (existing?.Answer == answer)
        {
            return _state.Current();
        }

        // An existing vote carries its row version, so a write by a stale activation is refused.
        var vote = existing?.CloneDetails() ?? new Vote { TimeOptionId = timeOptionId, UserId = memberId };
        vote.Answer = answer;
        var saved = await _votes.AddOrUpdateAsync(vote, v => v.TimeOptionId == timeOptionId && v.UserId == memberId);
        if (!saved.IsSuccess)
        {
            return WriteFailed(saved.Status);
        }

        _state.SetVote(saved.Value);
        return await PublishAsync();
    }

    /// <inheritdoc />
    public async Task<CommandResult> PickTimeAsync(Guid userId, long? timeOptionId)
    {
        if ((_state.CheckAdmin(userId) ?? _state.CheckScheduleEnabled(userId)) is { } denied)
        {
            return denied;
        }

        if (timeOptionId is { } id && _state.FindTimeOption(id) == null)
        {
            return TimeOptionNotFound();
        }

        var update = _state.Arrangement.CloneDetails();
        if (update.FinalTimeOptionId == timeOptionId)
        {
            return _state.Current();
        }

        update.FinalTimeOptionId = timeOptionId;
        return await SaveArrangementAsync(update);
    }

    /// <inheritdoc />
    public async Task<CommandResult> AddExpenseAsync(Guid userId, ExpenseRequest request)
    {
        if (_state.CheckExpensesEnabled(userId) is { } denied)
        {
            return denied;
        }

        if (_state.Arrangement.Expenses.Count >= ExpenseRules.MaxExpenses)
        {
            return CommandResult.Failure(CommandError.Invalid, $"An arrangement can have at most {ExpenseRules.MaxExpenses} expenses.");
        }

        var expense = new Expense { ArrangementId = _state.Arrangement.Id };
        if (Fill(expense, request) is { } invalid)
        {
            return invalid;
        }

        var added = await _expenses.AddAsync(expense);
        if (!added.IsSuccess)
        {
            return WriteFailed(added.Status);
        }

        _state.SetExpense(added.Value);
        return await PublishAsync();
    }

    /// <inheritdoc />
    public async Task<CommandResult> UpdateExpenseAsync(Guid userId, long expenseId, ExpenseRequest request)
    {
        if (_state.CheckExpensesEnabled(userId) is { } denied)
        {
            return denied;
        }

        if (_state.FindExpense(expenseId) is not { } existing)
        {
            return ExpenseNotFound();
        }

        if (existing.PaidByUserId != _state.FindMember(userId)!.UserId && !_state.FindMember(userId)!.IsAdmin)
        {
            return CommandResult.Failure(CommandError.Forbidden, "Only the member who paid or an admin can edit this expense.");
        }

        // The copy carries the row version, so a write by a stale activation is refused.
        var expense = existing.CloneDetails();
        if (Fill(expense, request) is { } invalid)
        {
            return invalid;
        }

        var saved = await _expenses.ReplaceAsync(expense);
        if (!saved.IsSuccess)
        {
            return WriteFailed(saved.Status);
        }

        _state.SetExpense(saved.Value);
        return await PublishAsync();
    }

    /// <inheritdoc />
    public async Task<CommandResult> DeleteExpenseAsync(Guid userId, long expenseId)
    {
        if (_state.CheckExpensesEnabled(userId) is { } denied)
        {
            return denied;
        }

        if (_state.FindExpense(expenseId) is not { } existing)
        {
            return ExpenseNotFound();
        }

        if (existing.PaidByUserId != _state.FindMember(userId)!.UserId && !_state.FindMember(userId)!.IsAdmin)
        {
            return CommandResult.Failure(CommandError.Forbidden, "Only the member who paid or an admin can delete this expense.");
        }

        // The database cascades the delete to the participations.
        var deleted = await _expenses.DeleteAsync(e => e.Id == expenseId);
        if (!deleted.IsSuccess || deleted.Value.Count == 0)
        {
            return WriteFailed(deleted.IsSuccess ? OperationStatus.NotFound : deleted.Status);
        }

        _state.RemoveExpense(expenseId);
        return await PublishAsync();
    }

    /// <inheritdoc />
    public async Task<CommandResult> SetBaseCurrencyAsync(Guid userId, string currency)
    {
        if (_state.CheckAdmin(userId) is { } denied)
        {
            return denied;
        }

        if (!Currencies.IsKnown(currency))
        {
            return CommandResult.Failure(CommandError.Invalid, "Unknown currency.");
        }

        var update = _state.Arrangement.CloneDetails();
        if (update.BaseCurrency == currency)
        {
            return _state.Current();
        }

        update.BaseCurrency = currency;
        var saved = await _arrangements.ChangeBaseCurrencyAsync(update, _state.Arrangement.BaseCurrency);
        if (!saved.IsSuccess)
        {
            return WriteFailed(saved.Status);
        }

        var rates = saved.Value.ExchangeRates;
        _state.ReplaceArrangement(saved.Value);
        _state.ReplaceExchangeRates(rates);
        return await PublishAsync();
    }

    /// <inheritdoc />
    public async Task<CommandResult> SetExchangeRateAsync(Guid userId, string currency, SetExchangeRateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_state.CheckAdmin(userId) is { } denied)
        {
            return denied;
        }

        if (_state.CheckExpensesEnabled(userId) is { } expensesDenied)
        {
            return expensesDenied;
        }

        if (!Currencies.IsKnown(currency) || currency == _state.Arrangement.BaseCurrency)
        {
            return CommandResult.Failure(CommandError.Invalid, "Choose a known currency other than the base currency.");
        }

        var existing = _state.FindExchangeRate(currency);

        var rate = request.Rate;

        // A currency added without a rate is seeded with the latest market rate when one is available.
        if (existing == null && rate == null)
        {
            rate = await SuggestRateAsync(currency);
        }

        if (((rate is { } value ? ExpenseRules.ValidateRate(value) : null) ?? ExpenseRules.ValidateLoss(request.LossPercent)) is { } invalid)
        {
            return CommandResult.Failure(CommandError.Invalid, invalid);
        }

        if (existing != null && existing.Rate == rate && existing.LossPercent == request.LossPercent)
        {
            return _state.Current();
        }

        // An existing rate carries its row version, so a write by a stale activation is refused.
        var arrangementId = _state.Arrangement.Id;
        var update = existing?.CloneDetails() ?? new ExchangeRate { ArrangementId = arrangementId, Currency = currency };
        update.Rate = rate;
        update.LossPercent = request.LossPercent;
        var saved = await _rates.AddOrUpdateAsync(update, r => r.ArrangementId == arrangementId && r.Currency == currency);
        if (!saved.IsSuccess)
        {
            return WriteFailed(saved.Status);
        }

        _state.SetExchangeRate(saved.Value);
        return await PublishAsync();
    }

    /// <inheritdoc />
    public async Task<CommandResult> RemoveCurrencyAsync(Guid userId, string currency)
    {
        if (_state.CheckAdmin(userId) is { } denied)
        {
            return denied;
        }

        if (_state.CheckExpensesEnabled(userId) is { } expensesDenied)
        {
            return expensesDenied;
        }

        if (_state.FindExchangeRate(currency) is not { } existing)
        {
            return CommandResult.Failure(CommandError.NotFound, "The currency is not in the arrangement.");
        }

        if (_state.Arrangement.Expenses.Exists(e => e.Currency == currency))
        {
            return CommandResult.Failure(CommandError.Invalid, "The currency is used by expenses.");
        }

        var deleted = await _rates.DeleteAsync(r => r.Id == existing.Id);
        if (!deleted.IsSuccess || deleted.Value.Count == 0)
        {
            return WriteFailed(deleted.IsSuccess ? OperationStatus.NotFound : deleted.Status);
        }

        _state.RemoveExchangeRate(currency);
        return await PublishAsync();
    }

    /// <inheritdoc />
    public async Task<CommandResult> RefreshRatesAsync(Guid userId)
    {
        if (_state.CheckAdmin(userId) is { } denied)
        {
            return denied;
        }

        if (_state.CheckExpensesEnabled(userId) is { } expensesDenied)
        {
            return expensesDenied;
        }

        var currencies = _state.Arrangement.ExchangeRates.ConvertAll(r => r.Currency);
        if (currencies.Count == 0)
        {
            return _state.Current();
        }

        var arrangementId = _state.Arrangement.Id;
        var latest = await _rateProvider.GetRatesAsync(_state.Arrangement.BaseCurrency, currencies, forceRefresh: true);
        var changed = false;
        foreach (var currency in currencies)
        {
            if (!latest.TryGetValue(currency, out var newRate) || _state.FindExchangeRate(currency) is not { } existing || existing.Rate == newRate)
            {
                continue;
            }

            var update = existing.CloneDetails();
            update.Rate = newRate;
            var saved = await _rates.AddOrUpdateAsync(update, r => r.ArrangementId == arrangementId && r.Currency == currency);
            if (!saved.IsSuccess)
            {
                return WriteFailed(saved.Status);
            }

            _state.SetExchangeRate(saved.Value);
            changed = true;
        }

        return changed ? await PublishAsync() : _state.Current();
    }

    private async Task<decimal?> SuggestRateAsync(string currency)
    {
        var rates = await _rateProvider.GetRatesAsync(_state.Arrangement.BaseCurrency, [currency], forceRefresh: false);
        return rates.TryGetValue(currency, out var rate) ? rate : null;
    }

    private CommandResult? Fill(Expense expense, ExpenseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request = request with { Description = ArrangementRules.NormalizeText(request.Description) };
        if (ExpenseRules.Validate(request) is { } invalid)
        {
            return CommandResult.Failure(CommandError.Invalid, invalid);
        }

        // An edited expense may keep a currency that was since removed.
        if (request.Currency != expense.Currency && request.Currency != _state.Arrangement.BaseCurrency && _state.FindExchangeRate(request.Currency) == null)
        {
            return CommandResult.Failure(CommandError.Invalid, "Add the currency in the settings first.");
        }

        var payer = _state.FindMember(request.PaidByUserId);
        var participants = request.ParticipantUserIds.Select(_state.FindMember).ToList();
        if (payer == null || participants.Contains(null))
        {
            return CommandResult.Failure(CommandError.Invalid, "The payer and everyone sharing must be members.");
        }

        expense.Description = request.Description;
        expense.Amount = request.Amount;
        expense.Currency = request.Currency;
        expense.PaidByUserId = payer.UserId;
        expense.Date = request.Date;
        expense.IsSettlementPayment = request.IsSettlementPayment;
        expense.Participations = participants.Select(m => new ExpenseParticipation { UserId = m!.UserId }).ToList();
        return null;
    }

    private static CommandResult TimeOptionNotFound()
    {
        return CommandResult.Failure(CommandError.NotFound, "The time option does not exist. It may have been removed.");
    }

    private static CommandResult ExpenseNotFound()
    {
        return CommandResult.Failure(CommandError.NotFound, "The expense does not exist. It may have been removed.");
    }

    private static CommandResult UserNotFound()
    {
        return CommandResult.Failure(CommandError.NotFound, "The user does not exist.");
    }

    private async Task<CommandResult> PublishAsync()
    {
        var result = _state.Changed();
        await _notifier.ChangedAsync(result.Snapshot!);
        return result;
    }

    private CommandResult WriteFailed(OperationStatus status)
    {
        // The database no longer matches memory, so reload on the next call.
        DeactivateOnIdle();
        _logger.LogWarning("Arrangement {ArrangementId} write failed with {Status}. Reloading.", ArrangementId, status);
        return CommandResult.Failure(CommandError.Conflict, "Someone else changed this at the same time. Reload and try again.");
    }

    private async Task<CommandResult> SaveArrangementAsync(Arrangement update)
    {
        var saved = await _arrangements.UpdateAsync(update);
        if (!saved.IsSuccess)
        {
            return WriteFailed(saved.Status);
        }

        _state.ReplaceArrangement(saved.Value);
        return await PublishAsync();
    }

    private DateTime Now()
    {
        // Postgres stores microseconds. Truncate so memory matches what a reload would see.
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
