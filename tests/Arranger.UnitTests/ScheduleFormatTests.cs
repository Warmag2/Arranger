using System;
using AwesomeAssertions;
using Faemiyah.Arranger.Client.Services;
using Faemiyah.Arranger.Contracts.Arrangements;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class ScheduleFormatTests
{
    private static readonly TimeZoneInfo PlusTwo = TimeZoneInfo.CreateCustomTimeZone("PlusTwo", TimeSpan.FromHours(2), "PlusTwo", "PlusTwo");

    [Test]
    public void Describe_WholeDay_ShowsUtcDate()
    {
        var option = Option(new DateTime(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc), null, true);

        ScheduleFormat.Describe(option, TimeZoneInfo.CreateCustomTimeZone("Minus", TimeSpan.FromHours(-5), "Minus", "Minus"))
            .Should().Be(("Fri 19.6.2026", "All day"));
    }

    [Test]
    public void Describe_Timed_ConvertsToLocal()
    {
        var begin = new DateTime(2026, 6, 19, 16, 0, 0, DateTimeKind.Utc);

        ScheduleFormat.Describe(Option(begin, null, false), PlusTwo).Should().Be(("Fri 19.6.2026", "from 18:00"));
        ScheduleFormat.Describe(Option(begin, begin.AddHours(3), false), PlusTwo).Should().Be(("Fri 19.6.2026", "18:00–21:00"));
        ScheduleFormat.Describe(Option(begin, begin.AddHours(8), false), PlusTwo).Should().Be(("Fri 19.6.2026", "18:00–02:00 (+1)"));
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
