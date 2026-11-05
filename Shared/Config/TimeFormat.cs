using System;
using System.Globalization;

namespace SensEssentials;

/// <summary>
/// The one time format used by every setting: HH:MM:SS, or D:HH:MM:SS with days in front. Hours may go past 23 in the
/// first form, so 24:00:00 is 24 hours, where TimeSpan.Parse would read it as 24 days. Nothing is shorter than a second.
/// </summary>
public static class TimeFormat
{
    public const string Help = "Use HH:MM:SS, or D:HH:MM:SS for days, for example 00:05:00 or 1:00:00:00.";

    public static bool TryParse(string? text, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Older configs write days as 1.00:00:00.
        text = text!.Trim();
        var dot = text.IndexOf('.');
        var colon = text.IndexOf(':');
        if (dot >= 0 && dot < colon)
            text = text.Substring(0, dot) + ":" + text.Substring(dot + 1);

        var parts = text.Split(':');
        if (parts.Length is not (3 or 4))
            return false;

        var numbers = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                return false;
        }

        var hasDays = parts.Length == 4;
        var days = hasDays ? numbers[0] : 0;
        var hours = numbers[parts.Length - 3];
        var minutes = numbers[parts.Length - 2];
        var seconds = numbers[parts.Length - 1];
        if (minutes > 59 || seconds > 59 || (hasDays && hours > 23))
            return false;

        try
        {
            time = new TimeSpan(days, hours, minutes, seconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    public static string Format(TimeSpan time)
    {
        var clock = $"{time.Hours:00}:{time.Minutes:00}:{time.Seconds:00}";
        return time.Days > 0 ? $"{time.Days}:{clock}" : clock;
    }
}
