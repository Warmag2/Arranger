using System;
using AwesomeAssertions;
using Faemiyah.Arranger.Client.Services;
using Faemiyah.Arranger.Contracts.Arrangements;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class ScheduleFormatTests
{
    private static readonly TimeZoneInfo MinusFive = TimeZoneInfo.CreateCustomTimeZone("MinusFive", TimeSpan.FromHours(-5), "MinusFive", "MinusFive");
    private static readonly TimeZoneInfo PlusTwo = TimeZoneInfo.CreateCustomTimeZone("PlusTwo", TimeSpan.FromHours(2), "PlusTwo", "PlusTwo");
    private static readonly DateTime TimedBegin = new(2026, 6, 19, 16, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WholeDayBegin = new(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc);

    [TestCase(null, null)]
    [TestCase(VoteAnswer.Yes, "yes")]
    [TestCase(VoteAnswer.Maybe, "maybe")]
    [TestCase(VoteAnswer.No, "no")]
    public void CssClass_MatchesAnswer(VoteAnswer? answer, string? expected)
    {
        ScheduleFormat.CssClass(answer).Should().Be(expected);
    }

    [Test]
    public void Describe_Timed_ShowsLocalStart()
    {
        var option = Option(TimedBegin, null, false);

        var lines = ScheduleFormat.Describe(option, PlusTwo);

        lines.Should().Equal(new ScheduleLine("Fri 19.6.2026", null), new ScheduleLine(null, "from 18:00"));
    }

    [Test]
    public void Describe_TimedOverMidnight_ShowsBothDatesWithSeparator()
    {
        var option = Option(TimedBegin, TimedBegin.AddHours(8), false);

        var lines = ScheduleFormat.Describe(option, PlusTwo);

        lines.Should().Equal(new ScheduleLine("Fri 19.6.2026", "18:00"), new ScheduleLine(null, "to"), new ScheduleLine("Sat 20.6.2026", "02:00"));
    }

    [Test]
    public void Describe_TimedRange_ShowsLocalTimes()
    {
        var option = Option(TimedBegin, TimedBegin.AddHours(3), false);

        var lines = ScheduleFormat.Describe(option, PlusTwo);

        lines.Should().Equal(new ScheduleLine("Fri 19.6.2026", null), new ScheduleLine(null, "18:00–21:00"));
    }

    [Test]
    public void Describe_WholeDay_ShowsUtcDate()
    {
        var option = Option(WholeDayBegin, null, true);

        var lines = ScheduleFormat.Describe(option, MinusFive);

        lines.Should().Equal(new ScheduleLine("Fri 19.6.2026", null), new ScheduleLine(null, "All day"));
    }

    [Test]
    public void Describe_WholeDayRange_ShowsBothDatesWithSeparator()
    {
        var option = Option(WholeDayBegin, WholeDayBegin.AddDays(2), true);

        var lines = ScheduleFormat.Describe(option, MinusFive);

        lines.Should().Equal(new ScheduleLine("Fri 19.6.2026", null), new ScheduleLine(null, "to"), new ScheduleLine("Sun 21.6.2026", null));
    }

    [Test]
    public void DescribeInline_TimedOverMidnight_JoinsLinesOnOneLine()
    {
        var option = Option(TimedBegin, TimedBegin.AddHours(8), false);

        var text = ScheduleFormat.DescribeInline(option, PlusTwo);

        text.Should().Be("Fri 19.6.2026 18:00 – Sat 20.6.2026 02:00");
    }

    [Test]
    public void DescribeInline_WholeDayRange_JoinsLinesOnOneLine()
    {
        var option = Option(WholeDayBegin, WholeDayBegin.AddDays(2), true);

        var text = ScheduleFormat.DescribeInline(option, PlusTwo);

        text.Should().Be("Fri 19.6.2026 – Sun 21.6.2026");
    }

    [TestCase(null, VoteAnswer.Yes)]
    [TestCase(VoteAnswer.Yes, VoteAnswer.Maybe)]
    [TestCase(VoteAnswer.Maybe, VoteAnswer.No)]
    [TestCase(VoteAnswer.No, VoteAnswer.Yes)]
    public void Next_Cycles(VoteAnswer? answer, VoteAnswer expected)
    {
        ScheduleFormat.Next(answer).Should().Be(expected);
    }

    [TestCase(null, "")]
    [TestCase(VoteAnswer.Yes, "✓")]
    [TestCase(VoteAnswer.Maybe, "?")]
    [TestCase(VoteAnswer.No, "✗")]
    public void Symbol_MatchesAnswer(VoteAnswer? answer, string expected)
    {
        ScheduleFormat.Symbol(answer).Should().Be(expected);
    }

    private static TimeOptionSnapshot Option(DateTime begin, DateTime? end, bool isWholeDay)
    {
        return new TimeOptionSnapshot(1, begin, end, isWholeDay, Guid.NewGuid(), []);
    }
}
