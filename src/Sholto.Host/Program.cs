using Avalonia;
using System;

namespace Sholto.Host;

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
        // AppStackFactory.Create() composes SholtoStackFactory and runs Build() inside this factory lambda — BEFORE
        // Initialize() and OnFrameworkInitializationCompleted, ahead of even
        // StartWithClassicDesktopLifetime's own setup. Avalonia's docs say nothing
        // here may touch Avalonia types or expect a SynchronizationContext; every
        // leaf SholtoStack builds was checked against that rule — see its class doc.
        => AppBuilder.Configure(() => new Sholto.Interface.MainUI.App(new SholtoStartup(new AppStackFactory().Create())))
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
            // Skia's GPU resource cache defaults to 28 MB; a deck's baked waveform bitmap
            // larger than half of that is drawn tiled and re-uploaded every frame. The
            // ceiling comes from RenderingOptions (see there for the numbers).
            .With(new SkiaOptions
            {
                MaxGpuResourceSizeBytes = new SholtoOptions().Rendering.Value.MaxGpuResourceSizeBytes,
            })
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
