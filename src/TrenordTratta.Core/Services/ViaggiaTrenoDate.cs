using System.Globalization;

namespace TrenordTratta.Core.Services;

/// <summary>Conversioni di data/ora specifiche delle due API.</summary>
public static class ViaggiaTrenoDate
{
    private static readonly Lazy<TimeZoneInfo> RomeTz = new(() =>
    {
        foreach (var id in new[] { "Europe/Rome", "W. Europe Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Local;
    });

    public static TimeZoneInfo Rome => RomeTz.Value;

    /// <summary>Ora corrente a Roma (indipendente dal fuso del telefono).</summary>
    public static DateTime NowRome() => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Rome).DateTime;

    /// <summary>
    /// Formato "JavaScript" richiesto da /partenze: "Tue Nov 10 2026 18:30:00 GMT+0100".
    /// Nomi in inglese (cultura invariante), offset di Roma alla data indicata.
    /// </summary>
    public static string Format(DateTimeOffset when)
    {
        var local = TimeZoneInfo.ConvertTime(when, Rome);
        var offset = local.Offset;
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var abs = offset.Duration();
        return string.Create(CultureInfo.InvariantCulture,
            $"{local:ddd MMM dd yyyy HH:mm:ss} GMT{sign}{abs.Hours:00}{abs.Minutes:00}");
    }

    /// <summary>Millisecondi Unix → ora locale di Roma.</summary>
    public static DateTime FromUnixMs(long ms) =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(ms), Rome).DateTime;

    /// <summary>
    /// "HH:mm:ss" (o "HH:mm") sul giorno del treno. Se l'orario è "prima" del riferimento di oltre 12 ore
    /// si assume il passaggio di mezzanotte.
    /// </summary>
    public static DateTime? ParseTime(string? hhmmss, DateTime day, DateTime? reference = null)
    {
        if (string.IsNullOrWhiteSpace(hhmmss)) return null;
        if (!TimeSpan.TryParseExact(hhmmss.Trim(), [@"hh\:mm\:ss", @"hh\:mm", @"h\:mm"], CultureInfo.InvariantCulture, out var t))
            return null;

        var result = day.Date + t;
        if (reference is DateTime r && (r - result).TotalHours > 12)
            result = result.AddDays(1);
        return result;
    }
}
