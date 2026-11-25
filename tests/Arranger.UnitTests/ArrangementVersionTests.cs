using System;
using AwesomeAssertions;
using Faemiyah.Arranger.Contracts.Arrangements;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class ArrangementVersionTests
{
    private static readonly Guid Epoch = Guid.NewGuid();

    [Test]
    public void Supersedes_AnyOtherEpoch()
    {
        // A reactivated grain restarts its sequence, so any change of epoch wins.
        new ArrangementVersion(Epoch, 0).Supersedes(new ArrangementVersion(Guid.NewGuid(), 99)).Should().BeTrue();
    }

    [Test]
    public void Supersedes_Nothing()
    {
        new ArrangementVersion(Epoch, 2).Supersedes(null).Should().BeTrue();
    }

    [TestCase(1, true)]
    [TestCase(2, false)]
    [TestCase(3, false)]
    public void Supersedes_OnlyAnOlderSequenceInTheSameEpoch(long otherSequence, bool expected)
    {
        var version = new ArrangementVersion(Epoch, 2);

        var supersedes = version.Supersedes(new ArrangementVersion(Epoch, otherSequence));

        supersedes.Should().Be(expected);
    }
}
