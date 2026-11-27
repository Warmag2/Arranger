using System;
using AwesomeAssertions;
using Faemiyah.Arranger.Contracts.Arrangements;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class ArrangementRulesTests
{
    private static readonly DateTime Day = new(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public void NormalizeDetails_AcceptsTitleAtLimitAfterTrimming()
    {
        ArrangementRules.NormalizeDetails(new string('a', ArrangementRules.TitleMaxLength) + " ", null).Error.Should().BeNull();
    }

    [Test]
    public void NormalizeDetails_RejectsBlankTitle()
    {
        ArrangementRules.NormalizeDetails("   ", "x").Error.Should().NotBeNull();
    }

    [Test]
    public void NormalizeDetails_TrimsText()
    {
        ArrangementRules.NormalizeDetails(" Sauna ", null).Should().Be(("Sauna", string.Empty, (string?)null));
    }

    [TestCase(null, "")]
    [TestCase("  Sauna \n", "Sauna")]
    public void NormalizeText_Trims(string? text, string expected)
    {
        ArrangementRules.NormalizeText(text).Should().Be(expected);
    }

    [Test]
    public void TruncateToMinute_KeepsKind()
    {
        var truncated = ArrangementRules.TruncateToMinute(new DateTime(2026, 6, 19, 18, 30, 45, 123, DateTimeKind.Utc));

        truncated.Should().Be(new DateTime(2026, 6, 19, 18, 30, 0, DateTimeKind.Utc));
        truncated.Kind.Should().Be(DateTimeKind.Utc);
    }

    [TestCase(ArrangementRules.TitleMaxLength, ArrangementRules.DescriptionMaxLength)]
    [TestCase(1, 0)]
    public void ValidateDetails_AcceptsLimits(int titleLength, int descriptionLength)
    {
        ArrangementRules.ValidateDetails(new string('a', titleLength), new string('b', descriptionLength)).Should().BeNull();
    }

    [TestCase(0, 0)]
    [TestCase(ArrangementRules.TitleMaxLength + 1, 0)]
    [TestCase(1, ArrangementRules.DescriptionMaxLength + 1)]
    public void ValidateDetails_RejectsEmptyAndTooLong(int titleLength, int descriptionLength)
    {
        ArrangementRules.ValidateDetails(new string('a', titleLength), new string('b', descriptionLength)).Should().NotBeNull();
    }

    [TestCase(0, null, true)]
    [TestCase(0, 48, true)]
    [TestCase(18, null, false)]
    [TestCase(18, 26, false)]
    public void ValidateTimeOption_AcceptsValid(int beginHours, int? endHours, bool isWholeDay)
    {
        ArrangementRules.ValidateTimeOption(Day.AddHours(beginHours), DayAt(endHours), isWholeDay).Should().BeNull();
    }

    [TestCase(1, null, true)]
    [TestCase(0, 30, true)]
    [TestCase(0, 0, true)]
    [TestCase(18, 18, false)]
    public void ValidateTimeOption_RejectsInvalid(int beginHours, int? endHours, bool isWholeDay)
    {
        ArrangementRules.ValidateTimeOption(Day.AddHours(beginHours), DayAt(endHours), isWholeDay).Should().NotBeNull();
    }

    [Test]
    public void ValidateTimeOption_RejectsNonUtc()
    {
        ArrangementRules.ValidateTimeOption(DateTime.SpecifyKind(Day, DateTimeKind.Local), null, true).Should().NotBeNull();
    }

    [Test]
    public void ValidateTimeOption_RejectsYearOutOfRange()
    {
        ArrangementRules.ValidateTimeOption(new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc), null, true).Should().NotBeNull();
    }

    private static DateTime? DayAt(int? hours)
    {
        return hours is null ? null : Day.AddHours(hours.Value);
    }
}
