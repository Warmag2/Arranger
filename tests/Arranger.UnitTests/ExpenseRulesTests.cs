using System;
using AwesomeAssertions;
using Faemiyah.Arranger.Contracts.Arrangements;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class ExpenseRulesTests
{
    private static readonly Guid A = new("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = new("00000000-0000-0000-0000-00000000000b");
    private static readonly decimal[] InvalidLosses = [-1, 1.005m, ExpenseRules.MaxLossPercent + 0.01m];
    private static readonly decimal[] InvalidRates = [0, 0.000000001m, ExpenseRules.MaxRate + 1];
    private static readonly ExpenseRequest Request = new("Sushi", 2, "JPY", A, [A, B], new DateOnly(2026, 6, 19), false);
    private static readonly decimal[] ValidLosses = [0, ExpenseRules.MaxLossPercent];
    private static readonly decimal[] ValidRates = [0.00000001m, ExpenseRules.MaxRate];

    [Test]
    public void Validate_AcceptsAValidExpense()
    {
        ExpenseRules.Validate(Request).Should().BeNull();
    }

    [Test]
    public void Validate_RefusesDuplicateParticipants()
    {
        ExpenseRules.Validate(Request with { ParticipantUserIds = [A, A] }).Should().NotBeNull();
    }

    [Test]
    public void Validate_RefusesTooManyDecimalsForTheCurrency()
    {
        ExpenseRules.Validate(Request with { Amount = 1.5m }).Should().Contain("decimals");
    }

    [Test]
    public void Validate_RefusesUnknownCurrency()
    {
        ExpenseRules.Validate(Request with { Currency = "XXX" }).Should().NotBeNull();
    }

    [TestCaseSource(nameof(ValidLosses))]
    public void ValidateLoss_AcceptsZeroToMax(decimal lossPercent)
    {
        ExpenseRules.ValidateLoss(lossPercent).Should().BeNull();
    }

    [TestCaseSource(nameof(InvalidLosses))]
    public void ValidateLoss_RefusesNegativeHugeAndOverPrecise(decimal lossPercent)
    {
        ExpenseRules.ValidateLoss(lossPercent).Should().NotBeNull();
    }

    [TestCaseSource(nameof(ValidRates))]
    public void ValidateRate_AcceptsSmallestToMax(decimal rate)
    {
        ExpenseRules.ValidateRate(rate).Should().BeNull();
    }

    [TestCaseSource(nameof(InvalidRates))]
    public void ValidateRate_RefusesNonPositiveHugeAndOverPrecise(decimal rate)
    {
        ExpenseRules.ValidateRate(rate).Should().NotBeNull();
    }
}
