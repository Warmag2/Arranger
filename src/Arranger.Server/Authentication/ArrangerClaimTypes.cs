namespace Faemiyah.Arranger.Server.Authentication;

/// <summary>
/// Custom claim types in the authentication cookie.
/// </summary>
public static class ArrangerClaimTypes
{
    /// <summary>
    /// The user's security stamp at sign-in. The cookie is rejected once the stored stamp changes.
    /// </summary>
    public const string SecurityStamp = "arranger:security-stamp";
}
