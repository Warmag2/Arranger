using Faemiyah.Arranger.Data.Entities;

namespace Faemiyah.Arranger.Server.Authentication;

/// <summary>
/// The outcome of an <see cref="AccountService"/> operation.
/// </summary>
public enum AccountStatus
{
    /// <summary>
    /// The operation succeeded.
    /// </summary>
    Success,

    /// <summary>
    /// The user name has a wrong length or disallowed characters.
    /// </summary>
    InvalidUserName,

    /// <summary>
    /// The password has a wrong length.
    /// </summary>
    InvalidPassword,

    /// <summary>
    /// Another user already has the same user name, ignoring case.
    /// </summary>
    UserNameTaken,

    /// <summary>
    /// The current password was wrong.
    /// </summary>
    WrongPassword,

    /// <summary>
    /// The user no longer exists.
    /// </summary>
    UserNotFound,

    /// <summary>
    /// The user was changed concurrently; retry.
    /// </summary>
    Conflict,
}
