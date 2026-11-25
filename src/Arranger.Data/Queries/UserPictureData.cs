namespace Faemiyah.Arranger.Data.Queries;

/// <summary>
/// A user's profile picture.
/// </summary>
/// <param name="Data">The image bytes.</param>
/// <param name="ContentType">The MIME type of <paramref name="Data"/>.</param>
public sealed record UserPictureData(byte[] Data, string ContentType);