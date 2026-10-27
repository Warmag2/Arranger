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
    private static readonly Guid B = new("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid C = new("00000000-0000-0000-0000-00000000000c");
    private static readonly Guid D = new("00000000-0000-0000-0000-00000000000d");
    private static readonly DateOnly Day = new(2026, 6, 19);

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
    public void Balances_ArePaidMinusSharesPerCurrency()
    {
        var balances = ExpenseMath.Balances([Expense(30, "EUR", A, A, B, C), Expense(10, "EUR", B, A), Expense(900, "JPY", C, C, D)]);

        balances.Keys.Should().Equal("EUR", "JPY");
        balances["EUR"].Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [A] = 10, [B] = 0, [C] = -10 });
        balances["JPY"].Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [C] = 450, [D] = -450 });
    }

    [Test]
    public void SuggestPayments_LargestDebtorPaysLargestCreditor_UntilSettled()
    {
        // A is owed 75 and B 15; C and D owe 45 each, and C goes first by user ID.
        var expenses = new[] { Expense(100, "EUR", A, A, B, C, D), Expense(40, "EUR", B, C, D) };

        var payments = ExpenseMath.SuggestPayments(expenses, Day);

        payments.Select(p => (p.PaidByUserId, p.ParticipantUserIds.Single(), p.Amount)).Should().Equal((C, A, 45m), (D, A, 30m), (D, B, 15m));
        payments.Should().OnlyContain(p => p.IsSettlementPayment && p.Currency == "EUR" && p.Date == Day && ExpenseRules.Validate(p) == null);

        var settled = expenses.Concat(payments.Select(p => Expense(p.Amount, p.Currency, p.PaidByUserId, [.. p.ParticipantUserIds])));
        ExpenseMath.SuggestPayments(settled, Day).Should().BeEmpty();
    }

    [Test]
    public void SuggestPayments_AreNoneWithoutExpenses()
    {
        ExpenseMath.SuggestPayments([], Day).Should().BeEmpty();
    }

    [Test]
    public void InBaseCurrency_ConvertsAndSumsEveryCurrency()
    {
        // A paid 30 EUR for A, B and C; B paid 1800 JPY for B and C at 200 JPY per EUR, i.e. 9 EUR.
        var expenses = new[] { Expense(30, "EUR", A, A, B, C), Expense(1800, "JPY", B, B, C) };
        var rates = new Dictionary<string, ExchangeRateSnapshot> { ["JPY"] = new(200, 0), ["USD"] = new(1.25m, 0) };

        ExpenseMath.MissingRates(expenses, "EUR", new Dictionary<string, ExchangeRateSnapshot>()).Should().Equal("JPY");
        ExpenseMath.MissingRates(expenses, "EUR", rates).Should().BeEmpty();
        ExpenseMath.InBaseCurrency(expenses, "EUR", rates).Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [A] = 20, [B] = -5.5m, [C] = -14.5m });
        ExpenseMath.InBaseCurrency(ExpenseMath.Spent(expenses), "EUR", rates).Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [A] = 30, [B] = 9 });
    }

    [Test]
    public void InBaseCurrency_IsSettledByAPaymentInAnotherCurrency()
    {
        // C owes A 10 EUR and pays 12 USD at 1.25 USD per EUR, i.e. 9.60 EUR, which leaves 0.40 EUR in A's favour.
        var expenses = new[] { Expense(20, "EUR", A, A, C), Expense(12, "USD", C, A) };
        var balances = ExpenseMath.InBaseCurrency(expenses, "EUR", new Dictionary<string, ExchangeRateSnapshot> { ["USD"] = new(1.25m, 0) });

        balances.Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [A] = 0.4m, [C] = -0.4m });
        ExpenseMath.SuggestPayments(balances, "EUR", Day).Should().ContainSingle().Which.Amount.Should().Be(0.4m);
    }

    [Test]
    public void InBaseCurrency_IncludesTheConversionLoss()
    {
        // 1 EUR = 160 JPY, but losing 6.25 % on conversion means 1 EUR buys 150 JPY, so 1500 JPY cost 10 EUR.
        var expenses = new[] { Expense(1500, "JPY", A, A, B) };
        var rate = new ExchangeRateSnapshot(160, 6.25m);

        rate.EffectiveRate().Should().Be(150);
        ExpenseMath.InBaseCurrency(expenses, "EUR", new Dictionary<string, ExchangeRateSnapshot> { ["JPY"] = rate })
            .Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [A] = 5, [B] = -5 });
    }

    [Test]
    public void InBaseCurrency_ConvertsAnAmount_OrIsNullWithoutARate()
    {
        var rates = new Dictionary<string, ExchangeRateSnapshot> { ["JPY"] = new(160, 6.25m), ["USD"] = new(null, 0) };

        ExpenseMath.InBaseCurrency(1050, "JPY", "EUR", rates).Should().Be(7);
        ExpenseMath.InBaseCurrency(1000, "JPY", "EUR", rates).Should().Be(6.67m);
        ExpenseMath.InBaseCurrency(12.5m, "EUR", "EUR", rates).Should().Be(12.5m);
        ExpenseMath.InBaseCurrency(10, "USD", "EUR", rates).Should().BeNull();
        ExpenseMath.InBaseCurrency(10, "GBP", "EUR", rates).Should().BeNull();
    }

    [Test]
    public void SuggestPayments_IgnoreRoundingLeftovers()
    {
        var balances = new Dictionary<Guid, decimal> { [A] = 10.01m, [B] = -5, [C] = -5, [D] = 0.004m };

        var payments = ExpenseMath.SuggestPayments(balances, "EUR", Day);

        payments.Select(p => (p.PaidByUserId, p.ParticipantUserIds.Single(), p.Amount)).Should().Equal((B, A, 5m), (C, A, 5m));
    }

    [Test]
    public void ValidateRate_RefusesNonPositiveHugeAndOverPrecise()
    {
        ExpenseRules.ValidateRate(0.00000001m).Should().BeNull();
        ExpenseRules.ValidateRate(0).Should().NotBeNull();
        ExpenseRules.ValidateRate(0.000000001m).Should().NotBeNull();
        ExpenseRules.ValidateRate(ExpenseRules.MaxRate + 1).Should().NotBeNull();
        ExpenseRules.ValidateLoss(0).Should().BeNull();
        ExpenseRules.ValidateLoss(ExpenseRules.MaxLossPercent).Should().BeNull();
        ExpenseRules.ValidateLoss(-1).Should().NotBeNull();
        ExpenseRules.ValidateLoss(1.005m).Should().NotBeNull();
    }

    [Test]
    public void Validate_RefusesTooManyDecimalsForTheCurrency()
    {
        var request = new ExpenseRequest("Sushi", 1.5m, "JPY", A, [A, B], Day, false);

        ExpenseRules.Validate(request).Should().Contain("decimals");
        ExpenseRules.Validate(request with { Amount = 2 }).Should().BeNull();
        ExpenseRules.Validate(request with { ParticipantUserIds = [A, A] }).Should().NotBeNull();
        ExpenseRules.Validate(request with { Currency = "XXX" }).Should().NotBeNull();
    }

    [Test]
    public void Received_AreSharesPerCurrency_AndSpentMinusReceivedIsTheBalance()
    {
        var expenses = new[] { Expense(30, "EUR", A, A, B, C), Expense(10, "EUR", B, A), Expense(900, "JPY", C, C, D) };
        var received = ExpenseMath.Received(expenses);

        received.Keys.Should().Equal("EUR", "JPY");
        received["EUR"].Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [A] = 20, [B] = 10, [C] = 10 });
        received["JPY"].Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [C] = 450, [D] = 450 });

        var spent = ExpenseMath.Spent(expenses);
        var balances = ExpenseMath.Balances(expenses);
        foreach (var (currency, users) in balances)
        {
            foreach (var (userId, balance) in users)
            {
                (spent[currency].GetValueOrDefault(userId) - received[currency].GetValueOrDefault(userId)).Should().Be(balance);
            }
        }
    }

    [Test]
    public void Received_ArePaymentsToThem()
    {
        // B pays A 10 EUR in settlement, so A received 10 and B paid 10.
        var received = ExpenseMath.Received([new ExpenseSnapshot(0, "Payment", 10, "EUR", B, [A], Day, true)]);

        received["EUR"].Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [A] = 10 });
    }

    private static ExpenseSnapshot Expense(decimal amount, string currency, Guid paidBy, params Guid[] participants)
    {
        return new ExpenseSnapshot(0, "Test", amount, currency, paidBy, participants.Distinct().ToList(), Day, false);
    }
}
