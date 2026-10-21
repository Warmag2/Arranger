namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// Pick the final time, or clear the pick.
/// </summary>
/// <param name="TimeOptionId">The picked time option, or <b>null</b> to clear the pick.</param>
public sealed record PickTimeRequest(long? TimeOptionId);
