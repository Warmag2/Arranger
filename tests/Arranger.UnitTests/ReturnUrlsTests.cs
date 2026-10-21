using AwesomeAssertions;
using Faemiyah.Arranger.Server.Authentication;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class ReturnUrlsTests
{
    [TestCase("arrangements/1", "arrangements/1")]
    [TestCase("/arrangements/1?x=1", "arrangements/1?x=1")]
    [TestCase(null, "")]
    [TestCase("", "")]
    [TestCase("https://evil.example/", "")]
    [TestCase("//evil.example", "")]
    [TestCase("/\\evil.example", "")]
    [TestCase("\\\\evil.example", "")]
    public void ToLocalPath_KeepsOnlyLocalPaths(string? returnAddress, string expected)
    {
        ReturnUrls.ToLocalPath(returnAddress).Should().Be(expected);
    }
}
