using Avalonia;
using Avalonia.Headless;

namespace Sholto.Bench.Ui;

/// <summary>
/// Once-only headless Avalonia bootstrap for the <c>ui</c>/<c>screenshot</c>
/// subcommands — the Bench-side counterpart of
/// <c>tests/Sholto.App.Tests/AvaloniaTestApp.cs</c>. Same reason it exists: the
/// real <c>Sholto.App.App</c> is used as the configured application type (not a
/// bare <see cref="Application"/>) so <c>App.axaml</c>'s <c>FluentTheme</c> +
/// dark variant actually load — otherwise <c>ThemeContext</c>'s asset-loader
/// read and every templated control (ListBox, Button, …) come up unstyled.
/// <see cref="Sholto.App.App.OnFrameworkInitializationCompleted"/> — the method
/// that opens the DB, the audio device, and the MIDI controller — is never
/// called here; only <c>AppBuilder.SetupWithoutStarting()</c> runs, which
/// invokes just <c>Initialize()</c> (the XAML load). Bench builds its own
/// <c>MainViewModel</c>/<c>MainWindow</c> from fakes — see
/// <see cref="BenchAppComposer"/> — never that composition root.
///
/// <c>UseHeadlessDrawing = false</c> + <c>.UseSkia()</c> is what makes
/// <c>CaptureRenderedFrame</c> return real pixels instead of null — see the
/// screenshot subcommand.
/// </summary>
internal static class BenchHeadlessApp
{
    private static readonly Lazy<bool> Init = new(() =>
    {
        AppBuilder.Configure<Sholto.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        return true;
    });

    public static void EnsureStarted() => _ = Init.Value;
}
