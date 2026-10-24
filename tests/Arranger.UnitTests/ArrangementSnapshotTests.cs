using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Faemiyah.Arranger.Contracts.Arrangements;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class ArrangementSnapshotTests
{
    [TestCase(false, false, ArrangementTab.Schedule)]
    [TestCase(true, false, ArrangementTab.Expenses)]
    [TestCase(false, true, ArrangementTab.Schedule)]
    [TestCase(true, true, ArrangementTab.Schedule)]
    public void DefaultTab_IsTheOnlyEnabledOne(bool isScheduleDisabled, bool isExpensesDisabled, ArrangementTab expected)
    {
        var snapshot = new ArrangementSnapshot(
            Guid.NewGuid(),
            "Trip",
            string.Empty,
            Guid.NewGuid(),
            DateTime.UtcNow,
            true,
            true,
            "EUR",
            null,
            isScheduleDisabled,
            isExpensesDisabled,
            [],
            [],
            [],
            new Dictionary<string, ExchangeRateSnapshot>(),
            new ArrangementVersion(Guid.NewGuid(), 1));

        snapshot.DefaultTab().Should().Be(expected);
    }
}
