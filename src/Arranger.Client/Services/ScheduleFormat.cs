using System;
using System.Collections.Generic;
using System.Globalization;
using Faemiyah.Arranger.Contracts.Arrangements;

namespace Faemiyah.Arranger.Client.Services;

/// <summary>
/// One line of a time option's description. A line is either a date line, which carries a <see cref="Date"/> shown at
/// full size with an optional smaller <see cref="Time"/> on the same line, or a detail line, which has no
/// <see cref="Date"/> and shows only the smaller <see cref="Time"/> text, such as "All day", a same-day time range, or
/// the separator between two whole-day dates.
/// </summary>
/// <param name="Date">The date shown at full size, or <b>null</b> for a detail line.</param>
/// <param name="Time">The smaller text shown beside the date or on its own, or <b>null</b> when there is none.</param>
public readonly record struct ScheduleLine(string? Date, string? Time);

/// <summary>
/// How the schedule poll is shown.
/// </summary>
public static class ScheduleFormat
{
    /// <summary>
    /// Describe a time option for the viewer as a short stack of lines. Whole-day options show their UTC date; timed
    /// ones are converted to the viewer's time zone. Single-day options show the date on top with the time or "All day"
    /// smaller underneath. A multi-day timed option shows the start date and its time on one line and the end date and
    /// its time on the next, with a "to" separator between them, keeping the dates the same size and the times smaller.
    /// A multi-day whole-day option shows the two dates with the same "to" separator between them.
    /// </summary>
    /// <param name="option">The time option.</param>
    /// <param name="timeZone">The viewer's time zone.</param>
    /// <returns>The lines to show, from top to bottom.</returns>
    public static IReadOnlyList<ScheduleLine> Describe(TimeOptionSnapshot option, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (option.IsWholeDay)
        {
            return option.DateEnd is { } wholeEnd
                ? [new ScheduleLine(FormatDate(option.DateBegin), null), new ScheduleLine(null, "to"), new ScheduleLine(FormatDate(wholeEnd), null)]
                : [new ScheduleLine(FormatDate(option.DateBegin), null), new ScheduleLine(null, "All day")];
        }

        var begin = ToLocal(option.DateBegin, timeZone);
        if (option.DateEnd is not { } utcEnd)
        {
            return [new ScheduleLine(FormatDate(begin), null), new ScheduleLine(null, "from " + FormatTime(begin))];
        }

        var end = ToLocal(utcEnd, timeZone);
        return end.Date == begin.Date
            ? [new ScheduleLine(FormatDate(begin), null), new ScheduleLine(null, $"{FormatTime(begin)}–{FormatTime(end)}")]
            : [new ScheduleLine(FormatDate(begin), FormatTime(begin)), new ScheduleLine(null, "to"), new ScheduleLine(FormatDate(end), FormatTime(end))];
    }

    /// <summary>
    /// Describe a time option on a single line for use inside a sentence, e.g. "Fri 23.10.2026 16:00 – Sun 25.10.2026
    /// 12:00".
    /// </summary>
    /// <param name="option">The time option.</param>
    /// <param name="timeZone">The viewer's time zone.</param>
    /// <returns>The one-line description.</returns>
    public static string DescribeInline(TimeOptionSnapshot option, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (option.IsWholeDay)
        {
            return option.DateEnd is { } wholeEnd
                ? $"{FormatDate(option.DateBegin)} – {FormatDate(wholeEnd)}"
                : FormatDate(option.DateBegin);
        }

        var begin = ToLocal(option.DateBegin, timeZone);
        if (option.DateEnd is not { } utcEnd)
        {
            return $"{FormatDate(begin)} from {FormatTime(begin)}";
        }

        var end = ToLocal(utcEnd, timeZone);
        return end.Date == begin.Date
            ? $"{FormatDate(begin)} {FormatTime(begin)}–{FormatTime(end)}"
            : $"{FormatDate(begin)} {FormatTime(begin)} – {FormatDate(end)} {FormatTime(end)}";
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
