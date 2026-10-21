namespace Faemiyah.Arranger.Server.Hosting;

/// <summary>
/// Rate limiting policy names.
/// </summary>
public static class RateLimitingPolicies
{
    /// <summary>
    /// Login and registration form posts, limited per client IP.
    /// </summary>
    public const string Account = "account";
}
