using Avalonia;

namespace Sholto.Interface.MainUI.Tests;

/// <summary>
/// Shared, thread-safe, once-only Avalonia headless bootstrap for tests.
///
/// <see cref="Sholto.Interface.MainUI.Theming.ThemeContext"/>'s constructor eagerly reads
/// <c>Themes.Classic</c>, which loads the bundled theme JSON via Avalonia's
/// <c>AssetLoader</c> — that requires a live (even if unstarted) Avalonia
/// application. <c>Themes</c> is a static class: its type initializer runs
/// exactly once per process and a failed run is cached forever (a thrown
/// static constructor makes every later access re-throw
/// <see cref="TypeInitializationException"/>, even from a test that set
/// Avalonia up correctly). xUnit runs test classes in parallel by default, so
/// every test that constructs a <c>ThemeContext</c> must call
/// <see cref="EnsureStarted"/> first — <see cref="Lazy{T}"/>'s default
/// thread-safety mode blocks concurrent callers until the first one finishes,
/// so there is no window where a second thread can reach
/// <c>Themes.Classic</c> before setup completes.
/// </summary>
internal static class AvaloniaTestApp
{
    private static readonly Lazy<bool> Init = new(() =>
    {
        AppBuilder.Configure<Application>()
            .UsePlatformDetect()
            .SetupWithoutStarting();
        // Avalonia's asset loader fills its assembly cache on first use without a lock, so concurrent first
        // opens from parallel test classes intermittently miss. Open one asset here, inside the lock.
        Avalonia.Platform.AssetLoader.Open(new Uri("avares://Sholto/Themes/defaults.json")).Dispose();
        return true;
    });

    public static void EnsureStarted() => _ = Init.Value;
}
