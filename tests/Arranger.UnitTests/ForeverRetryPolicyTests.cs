using System;
using AwesomeAssertions;
using Faemiyah.Arranger.Client.Services;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class ForeverRetryPolicyTests
{
    [TestCase(0, 0)]
    [TestCase(1, 2)]
    [TestCase(3, 10)]
    [TestCase(4, 30)]
    [TestCase(1000, 30)]
    public void GetDelay_BacksOffAndNeverGivesUp(long previousAttempts, int expectedSeconds)
    {
        ForeverRetryPolicy.GetDelay(previousAttempts).Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }
}
