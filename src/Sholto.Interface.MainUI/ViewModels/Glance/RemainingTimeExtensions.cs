using System.Globalization;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>Formats a time left as the Glance countdown.</summary>
public static class RemainingTimeExtensions
{
    /// <summary>"−m:ss" for <paramref name="seconds"/>, whole seconds, never below zero.</summary>
    public static string ToRemainingText(this double seconds)
    {
        var whole = (int)Math.Max(0, Math.Floor(seconds));
        return string.Create(CultureInfo.InvariantCulture, $"−{whole / 60}:{whole % 60:00}");
    }
}
