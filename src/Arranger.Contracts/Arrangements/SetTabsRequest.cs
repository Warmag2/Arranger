using System;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// Enable or disable the schedule and expenses tabs.
/// </summary>
/// <param name="IsScheduleEnabled">Whether the schedule tab accepts changes.</param>
/// <param name="IsExpensesEnabled">Whether the expenses tab accepts changes.</param>
public sealed record SetTabsRequest(bool IsScheduleEnabled, bool IsExpensesEnabled);
