using System;
using AwesomeAssertions;
using Faemiyah.Arranger.Client.Services;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
[SetCulture("fi-FI")]
public class DisplayFormatTests
{
    [Test]
    public void Date_IgnoresCurrentCulture()
    {
        DisplayFormat.Date(new DateOnly(2026, 10, 3)).Should().Be("Sat 3.10.2026");
    }

    [Test]
    public void Money_GroupsWithSpacesAndUsesDecimalDot()
    {
        DisplayFormat.Money(23678889141.65m, "EUR").Should().Be("23\u00A0678\u00A0889\u00A0141.65 EUR");
    }
}
