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

    [Test]
    public void ReadDimensions_Png()
    {
        var data = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x02, 0x00,
        };

        ProfilePictureRules.ReadDimensions(data).Should().Be((1024, 512));
    }

    [Test]
    public void ReadDimensions_Jpeg()
    {
        var data = new byte[] { 0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x01, 0x00, 0x02, 0x00 };

        ProfilePictureRules.ReadDimensions(data).Should().Be((512, 256));
    }

    [Test]
    public void ReadDimensions_JpegSkipsSegments()
    {
        var data = new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0xAA, 0xBB, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0x10, 0x00, 0x20,
        };

        ProfilePictureRules.ReadDimensions(data).Should().Be((32, 16));
    }

    [Test]
    public void ReadDimensions_WebpExtended()
    {
        var data = new byte[]
        {
            0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x58,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x03, 0x00, 0xFF, 0x02, 0x00,
        };

        ProfilePictureRules.ReadDimensions(data).Should().Be((1024, 768));
    }

    [TestCase(new byte[] { 0x3C, 0x73 })]
    [TestCase(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
    public void ReadDimensions_ReturnsNullWhenUnknownOrTruncated(byte[] data)
    {
        ProfilePictureRules.ReadDimensions(data).Should().BeNull();
    }
}
