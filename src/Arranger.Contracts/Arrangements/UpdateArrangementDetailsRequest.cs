using System;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// Change the title and description of an arrangement.
/// </summary>
/// <param name="Title">The new title.</param>
/// <param name="Description">The new description, or <b>null</b> for none.</param>
public sealed record UpdateArrangementDetailsRequest(string Title, string? Description);
