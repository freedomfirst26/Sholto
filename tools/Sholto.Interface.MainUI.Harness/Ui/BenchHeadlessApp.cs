using Avalonia;
using Avalonia.Headless;

namespace Sholto.Interface.MainUI.Harness.Ui;

/// <summary>
/// Once-only headless Avalonia bootstrap for the <c>ui</c>/<c>screenshot</c>
/// subcommands — the Bench-side counterpart of
/// <c>tests/Sholto.Interface.MainUI.Tests/AvaloniaTestApp.cs</c>. Same reason it exists: the
/// real <c>Sholto.Interface.MainUI.App</c> is used as the configured application type (not a
/// bare <see cref="Application"/>) so <c>App.axaml</c>'s <c>FluentTheme</c> +
/// dark variant actually load — otherwise <c>ThemeContext</c>'s asset-loader
/// read and every templated control (ListBox, Button, …) come up unstyled.
/// <see cref="Sholto.Interface.MainUI.App.OnFrameworkInitializationCompleted"/> — the method
/// that opens the DB, the audio device, and the MIDI controller — is never
/// called here; only <c>AppBuilder.SetupWithoutStarting()</c> runs, which
/// invokes just <c>Initialize()</c> (the XAML load). Bench builds its own
/// <c>MainViewModel</c>/<c>MainWindow</c> from fakes — see
/// <see cref="BenchAppFactory"/> — never that factory.
///
/// <c>UseHeadlessDrawing = false</c> + <c>.UseSkia()</c> is what makes
/// <c>CaptureRenderedFrame</c> return real pixels instead of null — see the
/// screenshot subcommand.
///
/// <para>PROCESS INVARIANT: Avalonia allows <c>AppBuilder…SetupWithoutStarting()</c>
/// once per process, so the once-guard here is only correct while exactly ONE
/// instance of this class exists. <c>Program.Main</c> builds that one instance and
/// hands it down to the factory behind <c>BenchCli</c>'s UI host. A second
/// instance would run the setup again and Avalonia would throw.</para>
/// </summary>
public sealed class BenchHeadlessApp : IBenchHeadlessApp
{
    private readonly Lazy<bool> _init = new(() =>
    {
        AppBuilder.Configure<Sholto.Interface.MainUI.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        return true;
    });

    public void EnsureStarted() => _ = _init.Value;
}
