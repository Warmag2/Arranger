using System;
using System.Buffers.Binary;

namespace Faemiyah.Arranger.Contracts.Users;

/// <summary>
/// Limits and format detection for profile pictures.
/// </summary>
public static class ProfilePictureRules
{
    /// <summary>
    /// The maximum size of a stored picture in bytes.
    /// </summary>
    public const int MaxBytes = 256 * 1024;

    /// <summary>
    /// The width and height the client scales pictures down to before uploading.
    /// </summary>
    public const int UploadDimension = 256;

    /// <summary>
    /// The largest width or height a stored picture may have. Guards viewers against a small file that decodes to a
    /// huge bitmap (a decompression bomb).
    /// </summary>
    public const int MaxDimension = 1024;

    /// <summary>
    /// The accepted upload formats, for the file picker.
    /// </summary>
    public const string AcceptedContentTypes = "image/png,image/jpeg,image/webp";

    /// <summary>
    /// Detect the image format from the file signature.
    /// </summary>
    /// <remarks>
    /// The format is decided from the content, never from what the client claims, so that nothing but these images
    /// is ever served back from the picture endpoint.
    /// </remarks>
    /// <param name="data">The file content.</param>
    /// <returns>The MIME type, or <b>null</b> when the content is not PNG, JPEG or WebP.</returns>
    public static string? DetectContentType(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }

        if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }

        if (data.Length >= 12
            && data[..4].SequenceEqual("RIFF"u8)
            && data.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }

    /// <summary>
    /// Read a picture's pixel dimensions from its header, without decoding the pixels.
    /// </summary>
    /// <remarks>
    /// Only a few header bytes are inspected, so the data is never expanded into a bitmap. Use this before storing a
    /// picture to keep a small, highly compressed file from decoding to an enormous image in viewers' browsers.
    /// </remarks>
    /// <param name="data">The file content.</param>
    /// <returns>The width and height, or <b>null</b> when they cannot be read.</returns>
    public static (int Width, int Height)? ReadDimensions(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return ReadPngDimensions(data);
        }

        if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return ReadJpegDimensions(data);
        }

        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return ReadWebpDimensions(data);
        }

        return null;
    }

    private static (int Width, int Height)? ReadPngDimensions(ReadOnlySpan<byte> data)
    {
        // The IHDR chunk follows the 8-byte signature: a 4-byte length, "IHDR", then the width and height as big-endian ints.
        if (data.Length < 24 || !data.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            return null;
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(data.Slice(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(data.Slice(20, 4));
        return width > 0 && height > 0 ? (width, height) : null;
    }

    private static (int Width, int Height)? ReadJpegDimensions(ReadOnlySpan<byte> data)
    {
        // Walk the marker segments until a Start-Of-Frame, which carries the height and width.
        var pos = 2;
        while (pos + 1 < data.Length)
        {
            if (data[pos] != 0xFF)
            {
                return null;
            }

            var marker = data[pos + 1];
            if (marker == 0xFF)
            {
                pos++;
                continue;
            }

            // Markers without a payload: TEM and the standalone SOI/EOI/RSTn range.
            if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD9))
            {
                pos += 2;
                continue;
            }

            if (pos + 4 > data.Length)
            {
                return null;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos + 2, 2));
            var isStartOfFrame = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
            if (isStartOfFrame)
            {
                if (pos + 9 > data.Length)
                {
                    return null;
                }

                var height = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos + 5, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos + 7, 2));
                return width > 0 && height > 0 ? (width, height) : null;
            }

            pos += 2 + length;
        }

        return null;
    }

    private static (int Width, int Height)? ReadWebpDimensions(ReadOnlySpan<byte> data)
    {
        if (data.Length < 16)
        {
            return null;
        }

        var format = data.Slice(12, 4);
        if (format.SequenceEqual("VP8 "u8))
        {
            // Lossy: a 3-byte frame tag and the 0x9D 0x01 0x2A start code precede the 14-bit width and height.
            return data.Length < 30 ? null : ((data[26] | (data[27] << 8)) & 0x3FFF, (data[28] | (data[29] << 8)) & 0x3FFF);
        }

        if (format.SequenceEqual("VP8L"u8))
        {
            // Lossless: a 0x2F signature byte, then 14-bit width-1 and height-1.
            if (data.Length < 25 || data[20] != 0x2F)
            {
                return null;
            }

            var bits = (uint)(data[21] | (data[22] << 8) | (data[23] << 16) | (data[24] << 24));
            return ((int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
        }

        if (format.SequenceEqual("VP8X"u8))
        {
            // Extended: a flags block, then 24-bit canvas width-1 and height-1.
            return data.Length < 30
                ? null
                : ((data[24] | (data[25] << 8) | (data[26] << 16)) + 1, (data[27] | (data[28] << 8) | (data[29] << 16)) + 1);
        }

        return null;
    }
}
