using System;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// Open or close an arrangement for new members.
/// </summary>
/// <param name="IsOpen">Whether new members may join.</param>
public sealed record SetParticipationRequest(bool IsOpen);
