using System;
using System.Globalization;
using Faemiyah.Arranger.Contracts;

namespace Faemiyah.Arranger.Client.Services;

/// <summary>
/// The one place that turns dates, times and amounts into display text. None of it depends on the browser's locale.
/// </summary>
public static class DisplayFormat
{
    private const string DateFormat = "ddd d.M.yyyy";
    private const string TimeFormat = "HH:mm";

    private static readonly NumberFormatInfo MoneyNumberFormat = new() { NumberGroupSeparator = "\u00A0" };

    /// <summary>
    /// Format a calendar date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The date, such as "Sat 3.10.2026".</returns>
    public static string Date(DateOnly date)
    {
        return date.ToString(DateFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Format the date part of a moment.
    /// </summary>
    /// <param name="date">The moment, already in the time zone it is shown in.</param>
    /// <returns>The date, such as "Sat 3.10.2026".</returns>
    public static string Date(DateTime date)
    {
        return date.ToString(DateFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Format an amount with its currency's number of decimals and its code. Thousands are grouped with a no-break
    /// space and the decimal separator is a dot.
    /// </summary>
    /// <param name="value">The amount.</param>
    /// <param name="code">The currency code.</param>
    /// <returns>The amount, such as "1 234.50 EUR".</returns>
    public static string Money(decimal value, string code)
    {
        return $"{value.ToString("N" + Currencies.Decimals(code), MoneyNumberFormat)} {code}";
    }

    /// <summary>
    /// Format the time of day of a moment.
    /// </summary>
    /// <param name="time">The moment, already in the time zone it is shown in.</param>
    /// <returns>The time, such as "18:30".</returns>
    public static string Time(DateTime time)
    {
        return time.ToString(TimeFormat, CultureInfo.InvariantCulture);
    }
}
