using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Faemiyah.Repository.Entities;

namespace Faemiyah.Arranger.Data.Entities;

/// <summary>
/// A user's profile picture.
/// </summary>
/// <remarks>
/// Kept apart from <see cref="User"/> so that logins and settings changes never load the image.
/// </remarks>
[Index(nameof(UserId), IsUnique = true)]
public class UserPicture : EntityBase<long>
{
    /// <summary>
    /// The owning user's ID.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// The owning user.
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// The image bytes.
    /// </summary>
    [Required]
    public byte[] Data { get; set; } = [];

    /// <summary>
    /// The MIME type of <see cref="Data"/>, detected from its content.
    /// </summary>
    [Required]
    [MaxLength(32)]
    public string ContentType { get; set; } = string.Empty;
}
