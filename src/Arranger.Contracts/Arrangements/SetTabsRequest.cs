namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// Enable or disable the schedule and expenses tabs.
/// </summary>
/// <param name="IsScheduleDisabled">Whether the schedule tab refuses changes.</param>
/// <param name="IsExpensesDisabled">Whether the expenses tab refuses changes.</param>
public sealed record SetTabsRequest(bool IsScheduleDisabled, bool IsExpensesDisabled);
