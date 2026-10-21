using System;

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
}
