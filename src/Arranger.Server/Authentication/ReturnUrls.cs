using System;

namespace Faemiyah.Arranger.Server.Authentication;

/// <summary>
/// Sanitizes return addresses, so that a crafted link cannot redirect to another site after login.
/// </summary>
public static class ReturnUrls
{
    /// <summary>
    /// Reduce a return address to a base-relative path on this site.
    /// </summary>
    /// <param name="returnAddress">The requested return address.</param>
    /// <returns>A base-relative path without a leading slash, or an empty string for the home page.</returns>
    public static string ToLocalPath(string? returnAddress)
    {
        if (string.IsNullOrWhiteSpace(returnAddress)
            || !Uri.IsWellFormedUriString(returnAddress, UriKind.Relative)
            || returnAddress.Contains('\\', StringComparison.Ordinal)
            || returnAddress.StartsWith("//", StringComparison.Ordinal))
        {
            // "//host" is relative by the URI rules but means another host to a browser.
            return string.Empty;
        }

        return returnAddress.TrimStart('/');
    }
}
