using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using Faemiyah.Arranger.Contracts.Arrangements;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class ExpenseMathTests
{
    private static readonly Guid A = new("00000000-0000-0000-0000-00000000000a");
    private static readonly Dictionary<string, ExchangeRateSnapshot> AmountRates = new() { ["JPY"] = new(160, 6.25m), ["USD"] = new(null, 0) };
    private static readonly Guid B = new("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid C = new("00000000-0000-0000-0000-00000000000c");
    private static readonly Guid D = new("00000000-0000-0000-0000-00000000000d");
    private static readonly DateOnly Day = new(2026, 6, 19);

    // A is owed 75 and B 15; C and D owe 45 each, and C goes first by user ID.
    private static readonly ExpenseSnapshot[] DebtExpenses = [Expense(100, "EUR", A, A, B, C, D), Expense(40, "EUR", B, C, D)];

    private static readonly ExpenseSnapshot[] Expenses = [Expense(30, "EUR", A, A, B, C), Expense(10, "EUR", B, A), Expense(900, "JPY", C, C, D)];

    // A paid 30 EUR for A, B and C; B paid 1800 JPY for B and C at 200 JPY per EUR, i.e. 9 EUR.
    private static readonly ExpenseSnapshot[] MixedExpenses = [Expense(30, "EUR", A, A, B, C), Expense(1800, "JPY", B, B, C)];

    private static readonly Dictionary<string, ExchangeRateSnapshot> MixedRates = new() { ["JPY"] = new(200, 0), ["USD"] = new(1.25m, 0) };

    // C owes A 10 EUR and pays 12 USD at 1.25 USD per EUR, i.e. 9.60 EUR, which leaves 0.40 EUR in A's favour.
    private static readonly ExpenseSnapshot[] PartialPaymentExpenses = [Expense(20, "EUR", A, A, C), Expense(12, "USD", C, A)];

    [Test]
    public void Balances_ArePaidMinusSharesPerCurrency()
    {
        var balances = ExpenseMath.Balances(Expenses);

        balances.Keys.Should().Equal("EUR", "JPY");
        balances["EUR"].Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [A] = 10, [B] = 0, [C] = -10 });
        balances["JPY"].Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [C] = 450, [D] = -450 });
    }

    [Test]
    public void Balances_AreSpentMinusReceived()
    {
        var spent = ExpenseMath.Spent(Expenses);
        var received = ExpenseMath.Received(Expenses);

        var balances = ExpenseMath.Balances(Expenses);

        foreach (var (currency, users) in balances)
        {
            foreach (var (userId, balance) in users)
            {
                (spent[currency].GetValueOrDefault(userId) - received[currency].GetValueOrDefault(userId)).Should().Be(balance);
            }
        }
    }

    [Test]
    public void EffectiveRate_IncludesTheConversionLoss()
    {
        // 1 EUR = 160 JPY, but losing 6.25 % on conversion means 1 EUR buys 150 JPY.
        new ExchangeRateSnapshot(160, 6.25m).EffectiveRate().Should().Be(150);
    }

    [TestCase(1050, "JPY", 7)]
    [TestCase(1000, "JPY", 6.67)]
    [TestCase(12.5, "EUR", 12.5)]
    public void InBaseCurrency_ConvertsAnAmount(decimal amount, string currency, decimal expected)
    {
        ExpenseMath.InBaseCurrency(amount, currency, "EUR", AmountRates).Should().Be(expected);
    }

    [Test]
    public void InBaseCurrency_ConvertsAndSumsBalancesInEveryCurrency()
    {
        var balances = ExpenseMath.InBaseCurrency(MixedExpenses, "EUR", MixedRates);

        balances.Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [A] = 20, [B] = -5.5m, [C] = -14.5m });
    }

    [Test]
    public void InBaseCurrency_ConvertsAndSumsSpentInEveryCurrency()
    {
        var spent = ExpenseMath.Spent(MixedExpenses);

        var inBaseCurrency = ExpenseMath.InBaseCurrency(spent, "EUR", MixedRates);

        inBaseCurrency.Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [A] = 30, [B] = 9 });
    }

    [Test]
    public void InBaseCurrency_IncludesTheConversionLoss()
    {
        // At an effective rate of 150 JPY per EUR, 1500 JPY cost 10 EUR.
        var expenses = new[] { Expense(1500, "JPY", A, A, B) };
        var rates = new Dictionary<string, ExchangeRateSnapshot> { ["JPY"] = new(160, 6.25m) };

        var balances = ExpenseMath.InBaseCurrency(expenses, "EUR", rates);

        balances.Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [A] = 5, [B] = -5 });
    }

    [TestCase("GBP")]
    [TestCase("USD")]
    public void InBaseCurrency_IsNullWithoutARate(string currency)
    {
        ExpenseMath.InBaseCurrency(10, currency, "EUR", AmountRates).Should().BeNull();
    }

    [Test]
    public void InBaseCurrency_NetsAPaymentInAnotherCurrency()
    {
        var rates = new Dictionary<string, ExchangeRateSnapshot> { ["USD"] = new(1.25m, 0) };

        var balances = ExpenseMath.InBaseCurrency(PartialPaymentExpenses, "EUR", rates);

        balances.Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [A] = 0.4m, [C] = -0.4m });
    }

    [Test]
    public void InBaseCurrency_ThrowsArgumentExceptionWhenARateIsMissing()
    {
        var rates = new Dictionary<string, ExchangeRateSnapshot>();

        var act = () => ExpenseMath.InBaseCurrency(MixedExpenses, "EUR", rates);

        act.Should().Throw<ArgumentException>().WithParameterName("rates");
    }

    [Test]
    public void InBaseCurrency_ThrowsArgumentExceptionWhenARateIsUnknown()
    {
        var rates = new Dictionary<string, ExchangeRateSnapshot> { ["JPY"] = new(null, 0) };

        var act = () => ExpenseMath.InBaseCurrency(MixedExpenses, "EUR", rates);

        act.Should().Throw<ArgumentException>().WithParameterName("rates");
    }

    [Test]
    public void MissingRates_AreCurrenciesWithoutARate()
    {
        ExpenseMath.MissingRates(MixedExpenses, "EUR", new Dictionary<string, ExchangeRateSnapshot>()).Should().Equal("JPY");
    }

    [Test]
    public void MissingRates_AreNoneWhenEveryCurrencyHasARate()
    {
        ExpenseMath.MissingRates(MixedExpenses, "EUR", MixedRates).Should().BeEmpty();
    }

    [Test]
    public void Received_ArePaymentsToThem()
    {
        // B pays A 10 EUR in settlement, so A received 10 and B paid 10.
        var received = ExpenseMath.Received([new ExpenseSnapshot(0, "Payment", 10, "EUR", B, [A], Day, true)]);

        received["EUR"].Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [A] = 10 });
    }

    [Test]
    public void Received_AreSharesPerCurrency()
    {
        var received = ExpenseMath.Received(Expenses);

        received.Keys.Should().Equal("EUR", "JPY");
        received["EUR"].Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [A] = 20, [B] = 10, [C] = 10 });
        received["JPY"].Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [C] = 450, [D] = 450 });
    }

    [TestCase("EUR", 10, 3.34, 3.33)]
    [TestCase("EUR", 0.01, 0.01, 0)]
    [TestCase("JPY", 100, 34, 33)]
    [TestCase("KWD", 1, 0.334, 0.333)]
    public void Shares_GiveLeftoverUnitsInUserIdOrder(string currency, decimal amount, decimal first, decimal rest)
    {
        var shares = ExpenseMath.Shares(Expense(amount, currency, A, C, B, A));

        shares.Should().Equal(new Dictionary<Guid, decimal> { [A] = first, [B] = rest, [C] = rest });
    }

    [Test]
    public void SuggestPayments_AreNoneWithoutExpenses()
    {
        ExpenseMath.SuggestPayments([], Day).Should().BeEmpty();
    }

    [Test]
    public void SuggestPayments_AreValidSettlementPayments()
    {
        var payments = ExpenseMath.SuggestPayments(DebtExpenses, Day);

        payments.Should().OnlyContain(p => p.IsSettlementPayment && p.Currency == "EUR" && p.Date == Day && ExpenseRules.Validate(p) == null);
    }

    [Test]
    public void SuggestPayments_IgnoreRoundingLeftovers()
    {
        var balances = new Dictionary<Guid, decimal> { [A] = 10.01m, [B] = -5, [C] = -5, [D] = 0.004m };

        var payments = ExpenseMath.SuggestPayments(balances, "EUR", Day);

        payments.Select(p => (p.PaidByUserId, p.ParticipantUserIds.Single(), p.Amount)).Should().Equal((B, A, 5m), (C, A, 5m));
    }

    [Test]
    public void SuggestPayments_LargestDebtorPaysLargestCreditor()
    {
        var payments = ExpenseMath.SuggestPayments(DebtExpenses, Day);

        payments.Select(p => (p.PaidByUserId, p.ParticipantUserIds.Single(), p.Amount)).Should().Equal((C, A, 45m), (D, A, 30m), (D, B, 15m));
    }

    [Test]
    public void SuggestPayments_SettleTheDebts()
    {
        var payments = ExpenseMath.SuggestPayments(DebtExpenses, Day);
        var settled = DebtExpenses.Concat(payments.Select(p => Expense(p.Amount, p.Currency, p.PaidByUserId, [.. p.ParticipantUserIds])));

        var remaining = ExpenseMath.SuggestPayments(settled, Day);

        remaining.Should().BeEmpty();
    }

    [Test]
    public void SuggestPayments_SettleWhatAPaymentInAnotherCurrencyLeft()
    {
        var balances = ExpenseMath.InBaseCurrency(PartialPaymentExpenses, "EUR", new Dictionary<string, ExchangeRateSnapshot> { ["USD"] = new(1.25m, 0) });

        var payments = ExpenseMath.SuggestPayments(balances, "EUR", Day);

        payments.Should().ContainSingle().Which.Amount.Should().Be(0.4m);
    }

    private static ExpenseSnapshot Expense(decimal amount, string currency, Guid paidBy, params Guid[] participants)
    {
        return new ExpenseSnapshot(0, "Test", amount, currency, paidBy, participants.Distinct().ToList(), Day, false);
    }
}
