using System;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// Create a new arrangement.
/// </summary>
/// <param name="Title">The title.</param>
/// <param name="Description">The description, or <b>null</b> for none.</param>
/// <param name="IsScheduleEnabled">Whether the schedule tab is enabled.</param>
/// <param name="IsExpensesEnabled">Whether the expenses tab is enabled.</param>
public sealed record CreateArrangementRequest(string Title, string? Description, bool IsScheduleEnabled = true, bool IsExpensesEnabled = true);
