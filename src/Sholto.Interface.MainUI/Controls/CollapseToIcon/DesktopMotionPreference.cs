namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

/// <summary>Reduced motion, read once when built: the <c>SHOLTO_REDUCED_MOTION</c> variable ("1"/"true" or
/// "0"/"false") wins; otherwise the desktop's animations setting (off means reduced); otherwise motion
/// stays on. Avalonia has no reduced-motion API of its own.</summary>
public sealed class DesktopMotionPreference : IMotionPreference
{
    public const string EnvironmentVariable = "SHOLTO_REDUCED_MOTION";

    public DesktopMotionPreference(Func<string, string?> environment, IDesktopAnimationSetting setting)
    {
        var forced = environment(EnvironmentVariable)?.Trim().ToLowerInvariant();
        if (forced is "1" or "true") Reduced = true;
        else if (forced is "0" or "false") Reduced = false;
        else Reduced = setting.Read()?.Trim().ToLowerInvariant() == "false";
    }

    public bool Reduced { get; }
}
