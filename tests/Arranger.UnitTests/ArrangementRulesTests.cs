using System;
using AwesomeAssertions;
using Faemiyah.Arranger.Contracts.Arrangements;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class ArrangementRulesTests
{
    [TestCase(null, "")]
    [TestCase("  Sauna \n", "Sauna")]
    public void NormalizeText_Trims(string? text, string expected)
    {
        ArrangementRules.NormalizeText(text).Should().Be(expected);
    }

    [Test]
    public void ValidateDetails_AcceptsLimits()
    {
        ArrangementRules.ValidateDetails(new string('a', ArrangementRules.TitleMaxLength), new string('b', ArrangementRules.DescriptionMaxLength)).Should().BeNull();
        ArrangementRules.ValidateDetails("a", string.Empty).Should().BeNull();
    }

    [Test]
    public void ValidateDetails_RejectsEmptyAndTooLong()
    {
        ArrangementRules.ValidateDetails(string.Empty, string.Empty).Should().NotBeNull();
        ArrangementRules.ValidateDetails(new string('a', ArrangementRules.TitleMaxLength + 1), string.Empty).Should().NotBeNull();
        ArrangementRules.ValidateDetails("a", new string('b', ArrangementRules.DescriptionMaxLength + 1)).Should().NotBeNull();
    }

    [Test]
    public void TruncateToMinute_KeepsKind()
    {
        var truncated = ArrangementRules.TruncateToMinute(new DateTime(2026, 6, 19, 18, 30, 45, 123, DateTimeKind.Utc));

        truncated.Should().Be(new DateTime(2026, 6, 19, 18, 30, 0, DateTimeKind.Utc));
        truncated.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public void ValidateTimeOption_AcceptsValid()
    {
        var day = new DateTime(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc);

        ArrangementRules.ValidateTimeOption(day, null, true).Should().BeNull();
        ArrangementRules.ValidateTimeOption(day.AddHours(18), null, false).Should().BeNull();
        ArrangementRules.ValidateTimeOption(day.AddHours(18), day.AddHours(26), false).Should().BeNull();
    }

    [Test]
    public void ValidateTimeOption_RejectsInvalid()
    {
        var day = new DateTime(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc);

        ArrangementRules.ValidateTimeOption(DateTime.SpecifyKind(day, DateTimeKind.Local), null, true).Should().NotBeNull();
        ArrangementRules.ValidateTimeOption(new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc), null, true).Should().NotBeNull();
        ArrangementRules.ValidateTimeOption(day.AddHours(1), null, true).Should().NotBeNull();
        ArrangementRules.ValidateTimeOption(day, day.AddDays(1), true).Should().NotBeNull();
        ArrangementRules.ValidateTimeOption(day.AddHours(18), day.AddHours(18), false).Should().NotBeNull();
    }
}
