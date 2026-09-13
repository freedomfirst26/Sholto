using Avalonia;
using System;

namespace Sholto.App;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        // The entry point picks the world: LiveEntities = real FLX4, real window,
        // real view model. Avalonia's Configure<TApp>(Func<TApp>) overload is what
        // lets App take constructor dependencies at all — see ISholtoEntities.
        //
        // SholtoStack.Build() runs here, inside this factory lambda — BEFORE
        // Initialize() and OnFrameworkInitializationCompleted, ahead of even
        // StartWithClassicDesktopLifetime's own setup. Avalonia's docs say nothing
        // here may touch Avalonia types or expect a SynchronizationContext; every
        // leaf SholtoStack builds was checked against that rule — see its class doc.
        => AppBuilder.Configure(() => new App(new LiveEntities(), SholtoStack.Build()))
            .UsePlatformDetect()
            // Avalonia's X11 default is { Glx, Software } which often silently
            // falls back to Software on NVIDIA proprietary + Cinnamon. That kills
            // our render thread to ~1 fps doing CPU rasterisation of the waveforms.
            // Try Vulkan first (best on modern NVIDIA), then Egl (works around
            // NVIDIA's GLX quirks), then Glx, and Software only as absolute fallback.
            .With(new X11PlatformOptions
            {
                RenderingMode = new[]
                {
                    X11RenderingMode.Vulkan,
                    X11RenderingMode.Egl,
                    X11RenderingMode.Glx,
                    X11RenderingMode.Software,
                },
            })
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
