using System;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// Propose a time. See <see cref="ArrangementRules.ValidateTimeOption"/>.
/// </summary>
/// <param name="DateBegin">The start in UTC. For a whole-day option, the UTC midnight of the day.</param>
/// <param name="DateEnd">The end in UTC, or <b>null</b> when open-ended or whole-day.</param>
/// <param name="IsWholeDay">Whether the option is a whole day.</param>
public sealed record AddTimeOptionRequest(DateTime DateBegin, DateTime? DateEnd, bool IsWholeDay);
