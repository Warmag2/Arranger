namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// Open or close an arrangement for new time suggestions from non-admin members.
/// </summary>
/// <param name="IsAllowed">Whether non-admin members may suggest new times.</param>
public sealed record SetAllowNewTimesRequest(bool IsAllowed);
