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
    public void Supersedes_NothingAndOlderSequence()
    {
        var version = new ArrangementVersion(Epoch, 2);

        version.Supersedes(null).Should().BeTrue();
        version.Supersedes(new ArrangementVersion(Epoch, 1)).Should().BeTrue();
    }

    [Test]
    public void Supersedes_NotSameOrNewerSequence()
    {
        var version = new ArrangementVersion(Epoch, 2);

        version.Supersedes(new ArrangementVersion(Epoch, 2)).Should().BeFalse();
        version.Supersedes(new ArrangementVersion(Epoch, 3)).Should().BeFalse();
    }

    [Test]
    public void Supersedes_AnyOtherEpoch()
    {
        // A reactivated grain restarts its sequence, so any change of epoch wins.
        new ArrangementVersion(Epoch, 0).Supersedes(new ArrangementVersion(Guid.NewGuid(), 99)).Should().BeTrue();
    }
}
