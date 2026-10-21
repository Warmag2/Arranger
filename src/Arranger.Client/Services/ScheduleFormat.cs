using System;
using System.Globalization;
using Faemiyah.Arranger.Contracts.Arrangements;

namespace Faemiyah.Arranger.Client.Services;

/// <summary>
/// How the schedule poll is shown.
/// </summary>
public static class ScheduleFormat
{
    /// <summary>
    /// Describe a time option for the viewer. Whole-day options show their UTC date; timed ones are converted to the
    /// viewer's time zone. An end on a later day is marked with the number of days, e.g. <c>22:00–02:00 (+1)</c>.
    /// </summary>
    /// <param name="option">The time option.</param>
    /// <param name="timeZone">The viewer's time zone.</param>
    /// <returns>The date line and the time line.</returns>
    public static (string Date, string Time) Describe(TimeOptionSnapshot option, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (option.IsWholeDay)
        {
            return (FormatDate(option.DateBegin), "All day");
        }

        var begin = ToLocal(option.DateBegin, timeZone);
        if (option.DateEnd is not { } utcEnd)
        {
            return (FormatDate(begin), "from " + FormatTime(begin));
        }

        var end = ToLocal(utcEnd, timeZone);
        var days = (end.Date - begin.Date).Days;
        var suffix = days == 0 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $" (+{days})");
        return (FormatDate(begin), $"{FormatTime(begin)}–{FormatTime(end)}{suffix}");
    }

    /// <summary>
    /// The answer after the given one when a member taps their cell: none → yes → maybe → no → yes.
    /// </summary>
    /// <param name="answer">The current answer, or <b>null</b> for none.</param>
    /// <returns>The next answer.</returns>
    public static VoteAnswer Next(VoteAnswer? answer)
    {
        return answer switch
        {
            VoteAnswer.Yes => VoteAnswer.Maybe,
            VoteAnswer.Maybe => VoteAnswer.No,
            _ => VoteAnswer.Yes,
        };
    }

    /// <summary>
    /// The symbol shown in a cell.
    /// </summary>
    /// <param name="answer">The answer, or <b>null</b> for none.</param>
    /// <returns>The symbol, or an empty string for none.</returns>
    public static string Symbol(VoteAnswer? answer)
    {
        return answer switch
        {
            VoteAnswer.Yes => "✓",
            VoteAnswer.Maybe => "?",
            VoteAnswer.No => "✗",
            _ => string.Empty,
        };
    }

    private static DateTime ToLocal(DateTime utc, TimeZoneInfo timeZone)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone);
    }

    private static string FormatDate(DateTime date)
    {
        return date.ToString("ddd d.M.yyyy", CultureInfo.InvariantCulture);
    }

    private static string FormatTime(DateTime time)
    {
        return time.ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
