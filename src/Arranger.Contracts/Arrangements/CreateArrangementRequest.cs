namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// Create a new arrangement.
/// </summary>
/// <param name="Title">The title.</param>
/// <param name="Description">The description, or <b>null</b> for none.</param>
/// <param name="IsScheduleDisabled">Whether the schedule tab is disabled.</param>
/// <param name="IsExpensesDisabled">Whether the expenses tab is disabled.</param>
public sealed record CreateArrangementRequest(string Title, string? Description, bool IsScheduleDisabled = false, bool IsExpensesDisabled = false);
