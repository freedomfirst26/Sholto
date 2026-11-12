using Sholto.Data;
using Sholto.Interface.MainUI.Theming;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>Pins the line <see cref="SholtoThemeLoader"/> writes when a user theme is skipped because its name
/// collides with a bundled theme: tag <c>Themes</c>, <see cref="LogLevel.Warn"/>, the message as it was on the
/// console. Shares a collection with <see cref="SholtoThemeLoaderPathTests"/>, which also sets
/// <c>XDG_CONFIG_HOME</c>.</summary>
[Collection(SholtoThemeLoaderPathTests.XdgConfigHomeCollection)]
public class SholtoThemeLoaderLogTests
{
    /// <summary>Answers every JSON, bundled or user, with the same theme, so a user file always collides.</summary>
    private sealed class SameThemeFactory(SholtoTheme theme) : ISholtoThemeFactory
    {
        private readonly SholtoTheme _theme = theme;

        public SholtoTheme Create(string json) => _theme;
    }

    [Fact]
    public void A_user_theme_named_like_a_bundled_one_is_skipped_with_a_Themes_warning()
    {
        // The user themes folder is %APPDATA% on Windows, which the test must not write into.
        if (OperatingSystem.IsWindows()) return;

        AvaloniaTestApp.EnsureStarted();
        var theme = new ThemeStackFactory(new RecordingLogFactory()).Build().Catalog.All[0];
        var config = Directory.CreateTempSubdirectory("sholto-theme-log-");
        var saved = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", config.FullName);
        try
        {
            var logs = new RecordingLogFactory();
            var loader = new SholtoThemeLoader(new SameThemeFactory(theme), logs);
            Directory.CreateDirectory(loader.UserThemesDir());
            File.WriteAllText(Path.Combine(loader.UserThemesDir(), "copy.json"), "{}");

            loader.LoadAll();

            Assert.Contains(
                new LoggedLine("Themes", LogLevel.Warn,
                    $"skipping user theme '{theme.Name}': name collides with a bundled theme"),
                logs.Lines);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", saved);
            config.Delete(recursive: true);
        }
    }
}
