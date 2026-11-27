using System;
using System.Collections.Generic;
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
/// memory. A write the database refuses, or any exception, means memory may no longer match the database, so the
/// activation is dropped and the next call reloads. Every change is then pushed to the clients watching the
/// arrangement; calls are not reentrant, so the pushes leave in the order the changes happened.
/// </remarks>
public sealed class ArrangementGrain : Grain, IArrangementGrain, IIncomingGrainCallFilter
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
        var arrangement = await _arrangements.GetAsync(id, processIncludes: true, cancellationToken: cancellationToken);
        var profiles = arrangement == null ? [] : await _userQueries.GetProfilesAsync(ArrangementState.GetUserIds(arrangement), cancellationToken);
        _state.Load(arrangement, profiles);
        await base.OnActivateAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task Invoke(IIncomingGrainCallContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            await context.Invoke();
        }
        catch (Exception exception)
        {
            // A write may have committed before the exception, so memory may be stale. The caller logs the exception.
            DeactivateOnIdle();
            _logger.LogWarning("Arrangement {ArrangementId} call {Method} threw {ExceptionType}. Reloading.", ArrangementId, context.InterfaceMethod.Name, exception.GetType().Name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CommandResult> CreateAsync(Guid userId, CreateArrangementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_state.Exists)
        {
            return CommandResult.Failure(CommandError.Conflict, "The arrangement already exists.");
        }

        var (title, description, invalid) = ArrangementRules.NormalizeDetails(request.Title, request.Description);
        if (invalid != null)
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
            IsScheduleDisabled = request.IsScheduleDisabled,
            IsExpensesDisabled = request.IsExpensesDisabled,
            Members = [new ArrangementMember { UserId = creator.Id, JoinedAt = now, IsAdmin = true }],
        };

        return await CommitAsync(_arrangements.AddAsync(arrangement, a => a.UniqueId == id), added =>
        {
            _state.Load(added, [creator]);
            _logger.LogInformation("User {UserId} created arrangement {ArrangementId}.", userId, id);
        });
    }

    /// <inheritdoc />
    public Task<CommandResult> GetSnapshotAsync()
    {
        if (!_state.Exists)
        {
            // Do not keep an activation for every unknown ID someone asks for.
            DeactivateOnIdle();
        }

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
        return await CommitAsync(_members.AddAsync(member, m => m.ArrangementId == member.ArrangementId && m.UserId == member.UserId), added =>
        {
            _state.AddMember(added, profile);
            _logger.LogInformation("User {UserId} joined arrangement {ArrangementId}.", userId, ArrangementId);
        });
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

        return await CommitAsync(_members.DeleteWithVotesAsync(member), _ =>
        {
            _state.RemoveMember(member);
            _logger.LogInformation("User {UserId} left arrangement {ArrangementId}.", userId, ArrangementId);
        });
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
        if (update.IsScheduleDisabled == request.IsScheduleDisabled && update.IsExpensesDisabled == request.IsExpensesDisabled)
        {
            return _state.Current();
        }

        update.IsScheduleDisabled = request.IsScheduleDisabled;
        update.IsExpensesDisabled = request.IsExpensesDisabled;
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
        return await CommitAsync(_members.UpdateAsync(update, m => m.Id == update.Id), _state.ReplaceMember);
    }

    /// <inheritdoc />
    public async Task<CommandResult> UpdateDetailsAsync(Guid userId, UpdateArrangementDetailsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_state.CheckAdmin(userId) is { } denied)
        {
            return denied;
        }

        var (title, description, invalid) = ArrangementRules.NormalizeDetails(request.Title, request.Description);
        if (invalid != null)
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
        if (_state.CheckCanEditTimes(userId) is { } denied)
        {
            return denied;
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

        return await CommitAsync(_timeOptions.AddAsync(option), _state.AddTimeOption);
    }

    /// <inheritdoc />
    public async Task<CommandResult> RemoveTimeOptionAsync(Guid userId, long timeOptionId)
    {
        if (_state.CheckCanEditTimes(userId) is { } denied)
        {
            return denied;
        }

        if (_state.FindTimeOption(timeOptionId) == null)
        {
            return TimeOptionNotFound();
        }

        if (_state.Arrangement.FinalTimeOptionId == timeOptionId)
        {
            // Deleting the pick also changes the arrangement row, so save both together to keep its revision current.
            var update = _state.Arrangement.CloneDetails();
            update.FinalTimeOptionId = null;
            return await CommitAsync(_arrangements.RemovePickedTimeOptionAsync(update, timeOptionId), saved =>
            {
                _state.ReplaceArrangement(saved);
                _state.RemoveTimeOption(timeOptionId);
            });
        }

        // The database cascades the delete to the votes.
        return await CommitDeleteAsync(_timeOptions.DeleteAsync(o => o.Id == timeOptionId), () => _state.RemoveTimeOption(timeOptionId));
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
        return await CommitAsync(_votes.AddOrUpdateAsync(vote, v => v.TimeOptionId == timeOptionId && v.UserId == memberId), _state.SetVote);
    }

    /// <inheritdoc />
    public async Task<CommandResult> PickTimeAsync(Guid userId, long? timeOptionId)
    {
        if (_state.CheckCanPickTime(userId) is { } denied)
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
        ArgumentNullException.ThrowIfNull(request);
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

        return await CommitAsync(_expenses.AddAsync(expense), _state.SetExpense);
    }

    /// <inheritdoc />
    public async Task<CommandResult> UpdateExpenseAsync(Guid userId, long expenseId, ExpenseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_state.CheckExpensesEnabled(userId) is { } denied)
        {
            return denied;
        }

        if (_state.FindExpense(expenseId) is not { } existing)
        {
            return ExpenseNotFound();
        }

        if (!_state.CanEditExpense(userId, existing))
        {
            return CommandResult.Failure(CommandError.Forbidden, "Only the member who paid or an admin can edit this expense.");
        }

        // The copy carries the row version, so a write by a stale activation is refused.
        var expense = existing.CloneDetails();
        if (Fill(expense, request) is { } invalid)
        {
            return invalid;
        }

        return await CommitAsync(_expenses.ReplaceAsync(expense), _state.SetExpense);
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

        if (!_state.CanEditExpense(userId, existing))
        {
            return CommandResult.Failure(CommandError.Forbidden, "Only the member who paid or an admin can delete this expense.");
        }

        // The database cascades the delete to the participations.
        return await CommitDeleteAsync(_expenses.DeleteAsync(e => e.Id == expenseId), () => _state.RemoveExpense(expenseId));
    }

    /// <inheritdoc />
    public async Task<CommandResult> SetBaseCurrencyAsync(Guid userId, string currency)
    {
        if (_state.CheckCanManageCurrencies(userId) is { } denied)
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
        return await CommitAsync(_arrangements.ChangeBaseCurrencyAsync(update, _state.Arrangement.BaseCurrency), _state.ReplaceArrangementAndRates);
    }

    /// <inheritdoc />
    public async Task<CommandResult> SetExchangeRateAsync(Guid userId, string currency, SetExchangeRateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_state.CheckCanManageCurrencies(userId) is { } denied)
        {
            return denied;
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

        if (ExpenseRules.ValidateExchangeRate(rate, request.LossPercent) is { } invalid)
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
        return await CommitAsync(_rates.AddOrUpdateAsync(update, r => r.ArrangementId == arrangementId && r.Currency == currency), _state.SetExchangeRate);
    }

    /// <inheritdoc />
    public async Task<CommandResult> RemoveCurrencyAsync(Guid userId, string currency)
    {
        if (_state.CheckCanManageCurrencies(userId) is { } denied)
        {
            return denied;
        }

        if (_state.FindExchangeRate(currency) is not { } existing)
        {
            return CommandResult.Failure(CommandError.NotFound, "The currency is not in the arrangement.");
        }

        if (_state.Arrangement.Expenses.Exists(e => e.Currency == currency))
        {
            return CommandResult.Failure(CommandError.Invalid, "The currency is used by expenses.");
        }

        return await CommitDeleteAsync(_rates.DeleteAsync(r => r.Id == existing.Id), () => _state.RemoveExchangeRate(currency));
    }

    /// <inheritdoc />
    public async Task<CommandResult> RefreshRatesAsync(Guid userId)
    {
        if (_state.CheckCanManageCurrencies(userId) is { } denied)
        {
            return denied;
        }

        var currencies = _state.Arrangement.ExchangeRates.ConvertAll(r => r.Currency);
        if (currencies.Count == 0)
        {
            return _state.Current();
        }

        // A fetch, when the cache is stale, holds this grain's turn for up to the client timeout. Only this arrangement waits.
        var latest = await _rateProvider.GetRatesAsync(_state.Arrangement.BaseCurrency, currencies);
        var updates = new List<ExchangeRate>();
        foreach (var currency in currencies)
        {
            if (latest.TryGetValue(currency, out var newRate) && _state.FindExchangeRate(currency) is { } existing && existing.Rate != newRate)
            {
                var update = existing.CloneDetails();
                update.Rate = newRate;
                updates.Add(update);
            }
        }

        if (updates.Count == 0)
        {
            return _state.Current();
        }

        return await CommitAsync(_rates.UpdateManyAsync(updates, rate => r => r.Id == rate.Id), saved =>
        {
            foreach (var rate in saved)
            {
                _state.SetExchangeRate(rate);
            }
        });
    }

    private async Task<decimal?> SuggestRateAsync(string currency)
    {
        var rates = await _rateProvider.GetRatesAsync(_state.Arrangement.BaseCurrency, [currency]);
        return rates.TryGetValue(currency, out var rate) ? rate : null;
    }

    private CommandResult? Fill(Expense expense, ExpenseRequest request)
    {
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

    private async Task<CommandResult> CommitAsync<T>(Task<RepositoryResult<T>> write, Action<T> apply)
        where T : class
    {
        var written = await write;
        if (!written.IsSuccess)
        {
            return WriteFailed(written.Status);
        }

        apply(written.Value);
        return await PublishAsync();
    }

    private async Task<CommandResult> CommitDeleteAsync<T>(Task<RepositoryResult<IReadOnlyCollection<T>>> delete, Action apply)
        where T : class
    {
        var deleted = await delete;
        if (!deleted.IsSuccess || deleted.Value.Count == 0)
        {
            return WriteFailed(deleted.IsSuccess ? OperationStatus.NotFound : deleted.Status);
        }

        apply();
        return await PublishAsync();
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

    private Task<CommandResult> SaveArrangementAsync(Arrangement update)
    {
        return CommitAsync(_arrangements.UpdateAsync(update), _state.ReplaceArrangement);
    }

    private DateTime Now()
    {
        // Postgres stores microseconds. Truncate so memory matches what a reload would see.
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
