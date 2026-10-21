using System;
using System.Text.RegularExpressions;

namespace Faemiyah.Arranger.Contracts.Accounts;

/// <summary>
/// Validation rules for user names, passwords and settings, shared by the server and the client.
/// </summary>
public static partial class AccountRules
{
    /// <summary>
    /// The minimum length of a user name.
    /// </summary>
    public const int UserNameMinLength = 3;

    /// <summary>
    /// The maximum length of a user name.
    /// </summary>
    public const int UserNameMaxLength = 32;

    /// <summary>
    /// The allowed user name characters and length, as a regular expression. <c>\z</c> rather than <c>$</c>, because
    /// <c>$</c> also matches before a trailing newline.
    /// </summary>
    public const string UserNamePattern = @"^[A-Za-z0-9._-]{3,32}\z";

    /// <summary>
    /// The minimum length of a password.
    /// </summary>
    public const int PasswordMinLength = 8;

    /// <summary>
    /// The maximum length of a password.
    /// </summary>
    public const int PasswordMaxLength = 128;

    /// <summary>
    /// The maximum length of a time zone identifier. The longest IANA identifier is about 30 characters.
    /// </summary>
    public const int TimeZoneIdMaxLength = 64;

    /// <summary>
    /// Whether the user name has an allowed length and only allowed characters.
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <returns><b>true</b> when valid.</returns>
    public static bool IsValidUserName(string? userName)
    {
        return userName != null && UserNameRegex().IsMatch(userName);
    }

    /// <summary>
    /// Whether the password has an allowed length.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <returns><b>true</b> when valid.</returns>
    public static bool IsValidPassword(string? password)
    {
        return password != null && password.Length is >= PasswordMinLength and <= PasswordMaxLength;
    }

    /// <summary>
    /// Whether the time zone is a known IANA (or, on Windows servers, Windows) time zone identifier.
    /// </summary>
    /// <param name="timeZoneId">The time zone identifier.</param>
    /// <returns><b>true</b> when known.</returns>
    public static bool IsKnownTimeZone(string? timeZoneId)
    {
        return !string.IsNullOrEmpty(timeZoneId)
            && timeZoneId.Length <= TimeZoneIdMaxLength
            && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _);
    }

    /// <summary>
    /// Validate user settings.
    /// </summary>
    /// <param name="userName">The handle.</param>
    /// <param name="timeZoneId">The IANA time zone.</param>
    /// <param name="currency">The ISO 4217 default currency.</param>
    /// <returns>The error message, or <b>null</b> when valid.</returns>
    public static string? ValidateSettings(string? userName, string? timeZoneId, string? currency)
    {
        if (!IsValidUserName(userName))
        {
            return "The handle must be 3-32 letters, digits, dots, underscores or hyphens.";
        }

        if (!IsKnownTimeZone(timeZoneId))
        {
            return "The time zone is not known.";
        }

        return Currencies.IsKnown(currency) ? null : "The currency is not known.";
    }

    /// <summary>
    /// The case-insensitive form of a user name, matching the database's <c>upper()</c> for the allowed characters.
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <returns>The normalized user name.</returns>
    public static string NormalizeUserName(string userName)
    {
        ArgumentNullException.ThrowIfNull(userName);
        return userName.ToUpperInvariant();
    }

    [GeneratedRegex(UserNamePattern, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UserNameRegex();
}
