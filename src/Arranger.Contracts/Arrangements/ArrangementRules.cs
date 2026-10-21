using System;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// Limits and validation for arrangements, shared by the server and the client.
/// </summary>
public static class ArrangementRules
{
    /// <summary>
    /// The maximum length of a title.
    /// </summary>
    public const int TitleMaxLength = 200;

    /// <summary>
    /// The maximum length of a description.
    /// </summary>
    public const int DescriptionMaxLength = 2000;

    /// <summary>
    /// The maximum number of members.
    /// </summary>
    public const int MaxMembers = 100;

    /// <summary>
    /// The maximum number of time options.
    /// </summary>
    public const int MaxTimeOptions = 100;

    /// <summary>
    /// The earliest year a time option may start in. Catches missing values, which arrive as year 1.
    /// </summary>
    public const int MinTimeOptionYear = 2000;

    /// <summary>
    /// The latest year a time option may end in.
    /// </summary>
    public const int MaxTimeOptionYear = 2999;

    /// <summary>
    /// Trim a user-entered text, treating <b>null</b> as empty.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The normalized text.</returns>
    public static string NormalizeText(string? text)
    {
        return text?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Validate a normalized title and description.
    /// </summary>
    /// <param name="title">The normalized title.</param>
    /// <param name="description">The normalized description.</param>
    /// <returns>An error message, or <b>null</b> when valid.</returns>
    public static string? ValidateDetails(string title, string description)
    {
        if (title.Length == 0)
        {
            return "The title is required.";
        }

        if (title.Length > TitleMaxLength)
        {
            return $"The title can be at most {TitleMaxLength} characters long.";
        }

        if (description.Length > DescriptionMaxLength)
        {
            return $"The description can be at most {DescriptionMaxLength} characters long.";
        }

        return null;
    }

    /// <summary>
    /// Drop the seconds and anything smaller, which the UI never shows. Keeps the <see cref="DateTime.Kind"/>.
    /// </summary>
    /// <param name="time">The time.</param>
    /// <returns>The time truncated to the minute.</returns>
    public static DateTime TruncateToMinute(DateTime time)
    {
        return new DateTime(time.Ticks - (time.Ticks % TimeSpan.TicksPerMinute), time.Kind);
    }

    /// <summary>
    /// Validate a time option. Times are UTC instants. A whole-day option starts at a UTC midnight and has no end;
    /// a timed one may have an end, after its start.
    /// </summary>
    /// <param name="dateBegin">The start.</param>
    /// <param name="dateEnd">The end, or <b>null</b>.</param>
    /// <param name="isWholeDay">Whether the option is a whole day.</param>
    /// <returns>An error message, or <b>null</b> when valid.</returns>
    public static string? ValidateTimeOption(DateTime dateBegin, DateTime? dateEnd, bool isWholeDay)
    {
        if (dateBegin.Kind != DateTimeKind.Utc || dateEnd is { Kind: not DateTimeKind.Utc })
        {
            return "Times must be in UTC.";
        }

        if (dateBegin.Year < MinTimeOptionYear || (dateEnd ?? dateBegin).Year > MaxTimeOptionYear)
        {
            return $"Times must be between the years {MinTimeOptionYear} and {MaxTimeOptionYear}.";
        }

        if (isWholeDay)
        {
            return dateBegin.TimeOfDay == TimeSpan.Zero && dateEnd == null ? null : "A whole-day option is a date without times.";
        }

        return dateEnd <= dateBegin ? "The end must be after the start." : null;
    }
}
