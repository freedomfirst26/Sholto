using System.Diagnostics;

namespace Sholto.Interface.MainUI.Controls.CollapseToIcon;

/// <summary>Reads <c>org.gnome.desktop.interface enable-animations</c> (GNOME and Cinnamon) with
/// <c>gsettings</c>. Missing tool, missing schema, a hang or any failure reads as null.</summary>
public sealed class GsettingsAnimationSetting : IDesktopAnimationSetting
{
    private const int TimeoutMs = 1000;

    public string? Read()
    {
        try
        {
            var start = new ProcessStartInfo("gsettings", "get org.gnome.desktop.interface enable-animations")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var process = Process.Start(start);
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(TimeoutMs))
            {
                process.Kill();
                return null;
            }
            return process.ExitCode == 0 ? output.GetAwaiter().GetResult().Trim() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
