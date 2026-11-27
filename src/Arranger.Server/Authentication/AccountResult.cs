using Faemiyah.Arranger.Data.Entities;

namespace Faemiyah.Arranger.Server.Authentication;

/// <summary>
/// The status and, on success, the affected user.
/// </summary>
/// <param name="Status">The status.</param>
/// <param name="User">The user on success, otherwise <b>null</b>.</param>
public sealed record AccountResult(AccountStatus Status, User? User)
{
    /// <summary>
    /// A failed outcome, without a user.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The result.</returns>
    public static AccountResult Failed(AccountStatus status)
    {
        return new AccountResult(status, null);
    }
}
