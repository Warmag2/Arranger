using Faemiyah.Arranger.Data.Entities;

namespace Faemiyah.Arranger.Server.Authentication;

/// <summary>
/// The status and, on success, the affected user.
/// </summary>
/// <param name="Status">The status.</param>
/// <param name="User">The user on success, otherwise <b>null</b>.</param>
public sealed record AccountResult(AccountStatus Status, User? User);
