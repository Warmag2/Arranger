using System;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// Grant or revoke admin rights.
/// </summary>
/// <param name="IsAdmin">Whether the member should be an admin.</param>
public sealed record SetAdminRequest(bool IsAdmin);
