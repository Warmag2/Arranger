using AwesomeAssertions;
using Faemiyah.Arranger.Contracts.Users;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class ProfilePictureRulesTests
{
    [TestCase(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, "image/png")]
    [TestCase(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [TestCase(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    public void DetectContentType_RecognizesSignatures(byte[] data, string expected)
    {
        ProfilePictureRules.DetectContentType(data).Should().Be(expected);
    }

    [TestCase(new byte[0])]
    [TestCase(new byte[] { 0xFF, 0xD8 })]
    [TestCase(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x41, 0x56, 0x45 })]
    [TestCase(new byte[] { 0x3C, 0x73, 0x76, 0x67 })]
    public void DetectContentType_RejectsOthers(byte[] data)
    {
        ProfilePictureRules.DetectContentType(data).Should().BeNull();
    }
}
