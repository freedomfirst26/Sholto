using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>Pins the user themes folder per OS: on Linux <c>$XDG_CONFIG_HOME/sholto/themes</c>,
/// falling back to <c>~/.config/sholto/themes</c>, exactly as before; on Windows
/// <c>%APPDATA%\sholto\themes</c>. Tests that set <c>XDG_CONFIG_HOME</c> restore it.</summary>
public class SholtoThemeLoaderPathTests
{
    private sealed class UnusedThemeFactory : ISholtoThemeFactory
    {
        public SholtoTheme Create(string json) => throw new NotSupportedException();
    }

    private static string UserThemesDirWithXdg(string? xdg)
    {
        var saved = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", xdg);
        try { return new SholtoThemeLoader(new UnusedThemeFactory()).UserThemesDir(); }
        finally { Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", saved); }
    }

    private static string WindowsThemesDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "sholto", "themes");

    [Fact]
    public void UserThemesDir_honours_XDG_CONFIG_HOME_on_linux()
    {
        var expected = OperatingSystem.IsWindows()
            ? WindowsThemesDir()
            : Path.Combine("/tmp/xdg-config-test", "sholto", "themes");

        Assert.Equal(expected, UserThemesDirWithXdg("/tmp/xdg-config-test"));
    }

    [Fact]
    public void UserThemesDir_falls_back_to_dot_config_on_linux_without_XDG_CONFIG_HOME()
    {
        var expected = OperatingSystem.IsWindows()
            ? WindowsThemesDir()
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "sholto", "themes");

        Assert.Equal(expected, UserThemesDirWithXdg(null));
    }
}
