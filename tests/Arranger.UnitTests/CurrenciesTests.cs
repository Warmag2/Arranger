using AwesomeAssertions;
using Faemiyah.Arranger.Contracts;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class CurrenciesTests
{
    [Test]
    public void All_IsSortedAndUnique()
    {
        Currencies.All.Should().BeInAscendingOrder(c => c.Code, System.StringComparer.Ordinal);
        Currencies.All.Should().OnlyHaveUniqueItems(c => c.Code);
        Currencies.IsKnown(Currencies.Default).Should().BeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("eur")]
    [TestCase("XXX")]
    public void IsKnown_RejectsUnknownCodes(string? code)
    {
        Currencies.IsKnown(code).Should().BeFalse();
    }
}
