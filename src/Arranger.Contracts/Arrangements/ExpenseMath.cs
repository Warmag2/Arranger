using System;
using System.Collections.Generic;
using System.Linq;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// Splits, balances and suggested payments. Computed whenever needed, never stored.
/// </summary>
public static class ExpenseMath
{
    /// <summary>
    /// The description of a suggested payment.
    /// </summary>
    public const string PaymentDescription = "Payment";

    /// <summary>
    /// Split an expense equally. Shares are in the currency's smallest unit; leftover units go one each to the
    /// participants in user ID order, so the shares always add up to the amount.
    /// </summary>
    /// <param name="expense">The expense.</param>
    /// <returns>The share of each participant.</returns>
    public static IReadOnlyDictionary<Guid, decimal> Shares(ExpenseSnapshot expense)
    {
        ArgumentNullException.ThrowIfNull(expense);
        var unit = Unit(expense.Currency);
        var count = expense.ParticipantUserIds.Count;
        var share = decimal.Floor(expense.Amount / count / unit) * unit;
        var leftover = (int)((expense.Amount - share * count) / unit);
        return expense.ParticipantUserIds
            .Order()
            .Select((userId, index) => (userId, index))
            .ToDictionary(p => p.userId, p => p.index < leftover ? share + unit : share);
    }

    /// <summary>
    /// Each user's balance per currency: what they paid minus their shares. Positive means others owe them.
    /// </summary>
    /// <param name="expenses">The expenses.</param>
    /// <returns>The balances by currency and user.</returns>
    public static IReadOnlyDictionary<string, Dictionary<Guid, decimal>> Balances(IEnumerable<ExpenseSnapshot> expenses)
    {
        ArgumentNullException.ThrowIfNull(expenses);
        return SumByCurrency(expenses, e => Shares(e).Select(s => KeyValuePair.Create(s.Key, -s.Value)).Prepend(KeyValuePair.Create(e.PaidByUserId, e.Amount)));
    }

    /// <summary>
    /// What each user paid per currency, payments included. A user's balance is this minus their shares.
    /// </summary>
    /// <param name="expenses">The expenses.</param>
    /// <returns>The paid totals by currency and user.</returns>
    public static IReadOnlyDictionary<string, Dictionary<Guid, decimal>> Spent(IEnumerable<ExpenseSnapshot> expenses)
    {
        ArgumentNullException.ThrowIfNull(expenses);
        return SumByCurrency(expenses, e => [KeyValuePair.Create(e.PaidByUserId, e.Amount)]);
    }

    /// <summary>
    /// What each user received per currency: their shares of the expenses, payments to them included. A user's balance
    /// is their <see cref="Spent"/> minus this.
    /// </summary>
    /// <param name="expenses">The expenses.</param>
    /// <returns>The received totals by currency and user.</returns>
    public static IReadOnlyDictionary<string, Dictionary<Guid, decimal>> Received(IEnumerable<ExpenseSnapshot> expenses)
    {
        ArgumentNullException.ThrowIfNull(expenses);
        return SumByCurrency(expenses, Shares);
    }

    /// <summary>
    /// Payments that settle every balance in its own currency. See <see cref="SuggestPayments(IReadOnlyDictionary{Guid, decimal}, string, DateOnly)"/>.
    /// </summary>
    /// <param name="expenses">The expenses.</param>
    /// <param name="date">The date to give the payments.</param>
    /// <returns>The payments, ready to record, by currency.</returns>
    public static IReadOnlyList<ExpenseRequest> SuggestPayments(IEnumerable<ExpenseSnapshot> expenses, DateOnly date)
    {
        return [.. Balances(expenses).SelectMany(b => SuggestPayments(b.Value, b.Key, date))];
    }

    /// <summary>
    /// Payments that settle the balances: repeatedly the largest debtor pays the largest creditor, so at most one
    /// payment fewer than there are users is needed. Rounding leftovers smaller than the currency's unit are ignored.
    /// </summary>
    /// <param name="balances">The balances by user, in <paramref name="currency"/>.</param>
    /// <param name="currency">The currency.</param>
    /// <param name="date">The date to give the payments.</param>
    /// <returns>The payments, ready to record.</returns>
    public static IReadOnlyList<ExpenseRequest> SuggestPayments(IReadOnlyDictionary<Guid, decimal> balances, string currency, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(balances);
        var unit = Unit(currency);
        var open = balances.Where(u => Math.Abs(u.Value) >= unit).ToDictionary();
        var payments = new List<ExpenseRequest>();
        while (open.Values.Any(v => v < 0) && open.Values.Any(v => v > 0))
        {
            var debtor = open.OrderBy(u => u.Value).ThenBy(u => u.Key).First();
            var creditor = open.OrderByDescending(u => u.Value).ThenBy(u => u.Key).First();
            var amount = Math.Min(-debtor.Value, creditor.Value);
            payments.Add(new ExpenseRequest(PaymentDescription, amount, currency, debtor.Key, [creditor.Key], date, true));
            Settle(open, debtor.Key, amount, unit);
            Settle(open, creditor.Key, -amount, unit);
        }

        return payments;
    }

    /// <summary>
    /// The used currencies, other than the base currency, whose exchange rate is not known.
    /// </summary>
    /// <param name="expenses">The expenses.</param>
    /// <param name="baseCurrency">The base currency.</param>
    /// <param name="rates">The exchange rates to the base currency.</param>
    /// <returns>The currencies, in order.</returns>
    public static IReadOnlyList<string> MissingRates(IEnumerable<ExpenseSnapshot> expenses, string baseCurrency, IReadOnlyDictionary<string, ExchangeRateSnapshot> rates)
    {
        ArgumentNullException.ThrowIfNull(expenses);
        ArgumentNullException.ThrowIfNull(rates);
        return [.. expenses.Select(e => e.Currency).Where(c => c != baseCurrency && rates.GetValueOrDefault(c)?.Rate == null).Distinct().Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Each user's balance over all currencies, converted to the base currency with the conversion losses and rounded
    /// to its unit.
    /// </summary>
    /// <param name="expenses">The expenses, whose currencies all have a rate (see <see cref="MissingRates"/>).</param>
    /// <param name="baseCurrency">The base currency.</param>
    /// <param name="rates">The exchange rates to the base currency.</param>
    /// <returns>The balances by user.</returns>
    public static IReadOnlyDictionary<Guid, decimal> InBaseCurrency(IEnumerable<ExpenseSnapshot> expenses, string baseCurrency, IReadOnlyDictionary<string, ExchangeRateSnapshot> rates)
    {
        return InBaseCurrency(Balances(expenses), baseCurrency, rates);
    }

    /// <summary>
    /// Per-currency sums by user, such as <see cref="Balances"/> or <see cref="Spent"/>, converted to the base currency
    /// with the conversion losses and rounded to its unit.
    /// </summary>
    /// <param name="sums">The sums by currency and user, whose currencies all have a rate (see <see cref="MissingRates"/>).</param>
    /// <param name="baseCurrency">The base currency.</param>
    /// <param name="rates">The exchange rates to the base currency.</param>
    /// <returns>The sums by user.</returns>
    public static IReadOnlyDictionary<Guid, decimal> InBaseCurrency(IReadOnlyDictionary<string, Dictionary<Guid, decimal>> sums, string baseCurrency, IReadOnlyDictionary<string, ExchangeRateSnapshot> rates)
    {
        ArgumentNullException.ThrowIfNull(sums);
        ArgumentNullException.ThrowIfNull(rates);
        var total = new Dictionary<Guid, decimal>();
        foreach (var (currency, users) in sums)
        {
            var rate = RateOf(currency, baseCurrency, rates) ?? throw new ArgumentException($"{currency} has no rate.", nameof(rates));
            foreach (var (userId, balance) in users)
            {
                total[userId] = total.GetValueOrDefault(userId) + balance / rate;
            }
        }

        return total.ToDictionary(u => u.Key, u => RoundTo(baseCurrency, u.Value));
    }

    /// <summary>
    /// An amount converted to the base currency with the conversion loss and rounded to its unit.
    /// </summary>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The currency of the amount.</param>
    /// <param name="baseCurrency">The base currency.</param>
    /// <param name="rates">The exchange rates to the base currency.</param>
    /// <returns>The amount in the base currency, or <b>null</b> when the rate is not known.</returns>
    public static decimal? InBaseCurrency(decimal amount, string currency, string baseCurrency, IReadOnlyDictionary<string, ExchangeRateSnapshot> rates)
    {
        ArgumentNullException.ThrowIfNull(rates);
        return RateOf(currency, baseCurrency, rates) is { } rate ? RoundTo(baseCurrency, amount / rate) : null;
    }

    private static decimal? RateOf(string currency, string baseCurrency, IReadOnlyDictionary<string, ExchangeRateSnapshot> rates)
    {
        return currency == baseCurrency ? 1m : rates.GetValueOrDefault(currency)?.EffectiveRate();
    }

    private static decimal RoundTo(string currency, decimal value)
    {
        return decimal.Round(value, Currencies.Decimals(currency), MidpointRounding.AwayFromZero);
    }

    private static void Settle(Dictionary<Guid, decimal> open, Guid userId, decimal amount, decimal unit)
    {
        var balance = open[userId] + amount;
        if (Math.Abs(balance) < unit)
        {
            open.Remove(userId);
        }
        else
        {
            open[userId] = balance;
        }
    }

    private static SortedDictionary<string, Dictionary<Guid, decimal>> SumByCurrency(IEnumerable<ExpenseSnapshot> expenses, Func<ExpenseSnapshot, IEnumerable<KeyValuePair<Guid, decimal>>> amounts)
    {
        var sums = new SortedDictionary<string, Dictionary<Guid, decimal>>(StringComparer.Ordinal);
        foreach (var expense in expenses)
        {
            if (!sums.TryGetValue(expense.Currency, out var users))
            {
                users = [];
                sums[expense.Currency] = users;
            }

            foreach (var (userId, amount) in amounts(expense))
            {
                users[userId] = users.GetValueOrDefault(userId) + amount;
            }
        }

        return sums;
    }

    private static decimal Unit(string currency)
    {
        return 1m / (decimal)Math.Pow(10, Currencies.Decimals(currency));
    }
}
