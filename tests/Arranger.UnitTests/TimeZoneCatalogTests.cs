using System;
using System.Linq;
using AwesomeAssertions;
using Faemiyah.Arranger.Server.Users;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class TimeZoneCatalogTests
{
    private const string Berlin = "Europe/Berlin";

    [Test]
    public void GetTimeZones_FollowsDaylightSavingTimeBetweenCalls()
    {
        var clock = new SettableTimeProvider { Now = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero) };
        var catalog = new TimeZoneCatalog(clock);

        var winter = catalog.GetTimeZones().Single(z => z.Id == Berlin).Name;
        clock.Now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var summer = catalog.GetTimeZones().Single(z => z.Id == Berlin).Name;

        winter.Should().Be("(UTC+01:00) Europe/Berlin");
        summer.Should().Be("(UTC+02:00) Europe/Berlin");
    }

    private sealed class SettableTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow()
        {
            return Now;
        }
    }
}
