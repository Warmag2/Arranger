using AwesomeAssertions;
using Faemiyah.Arranger.Contracts.Accounts;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class AccountRulesTests
{
    [TestCase("abc")]
    [TestCase("Warma")]
    [TestCase("a.b_c-d")]
    [TestCase("12345678901234567890123456789012")]
    public void IsValidUserName_AcceptsAllowedNames(string userName)
    {
        AccountRules.IsValidUserName(userName).Should().BeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("ab")]
    [TestCase("123456789012345678901234567890123")]
    [TestCase("has space")]
    [TestCase("ääkkönen")]
    [TestCase("evil\nline")]
    [TestCase("abc\n")]
    public void IsValidUserName_RejectsOtherNames(string? userName)
    {
        AccountRules.IsValidUserName(userName).Should().BeFalse();
    }

    [TestCase(7, false)]
    [TestCase(8, true)]
    [TestCase(128, true)]
    [TestCase(129, false)]
    public void IsValidPassword_ChecksLength(int length, bool expected)
    {
        AccountRules.IsValidPassword(new string('x', length)).Should().Be(expected);
    }

    [Test]
    public void NormalizeUserName_UpperCasesInvariantly()
    {
        AccountRules.NormalizeUserName("Warma.i_9").Should().Be("WARMA.I_9");
    }
    [Test]
    public void ValidateSettings_AcceptsValidSettings()
    {
        AccountRules.ValidateSettings("Warma", "Europe/Helsinki", "EUR").Should().BeNull();
    }

    [TestCase("ab", "Europe/Helsinki", "EUR")]
    [TestCase("Warma", "Mars/Olympus", "EUR")]
    [TestCase("Warma", "Europe/Helsinki", "XXX")]
    [TestCase(null, null, null)]
    public void ValidateSettings_RejectsInvalidSettings(string? userName, string? timeZoneId, string? currency)
    {
        AccountRules.ValidateSettings(userName, timeZoneId, currency).Should().NotBeNull();
    }
}
