using Sholto.App;
using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Interface.Controller.Mappings;

using Sholto.Interface.MainUI;

namespace Sholto.Host;

/// <summary>One place that holds the app's configured options objects, so every
/// composition root — the real <c>App</c> and <c>tools/Sholto.Interface.Bench</c>'s
/// composers — constructs the same defaults instead of each declaring its own
/// <c>new XOptions()</c>.
///
/// <para>Bench already references <c>Sholto.Interface.MainUI</c>, which already references
/// <c>Sholto.Interface.Controller.Mappings</c>, so all four options types here are visible
/// from Bench today: no new project is needed to construct one there. The one
/// condition that would change that: if <c>Sholto.App.Audio</c> or <c>Sholto.Interface.Controller</c> ever needed to READ configured
/// values, they could not reference <c>Sholto.Interface.MainUI</c> without inverting the
/// onion. Not true today.</para></summary>
public sealed class SholtoOptions
{
    public IOptions<FeatureOptions> Feature { get; }
    public IOptions<ControllerMappingsOptions> Controllers { get; }
    public IOptions<ScratchOptions> Scratch { get; }
    public IOptions<MagnetismOptions> Magnetism { get; }
    public IOptions<GlanceOptions> Glance { get; }
    public IOptions<WaveformBandOptions> WaveformBands { get; }
    public IOptions<RenderingOptions> Rendering { get; }

    /// <summary>The app's configured options, at their defaults. Each composition
    /// root (Program, Bench's composers) builds one instance and hands it down.</summary>
    public SholtoOptions()
    {
        Feature = Options.Create(new FeatureOptions());
        Controllers = Options.Create(new ControllerMappingsOptions());
        Scratch = Options.Create(new ScratchOptions());
        Magnetism = Options.Create(new MagnetismOptions());
        Glance = Options.Create(new GlanceOptions());
        WaveformBands = Options.Create(new WaveformBandOptions());
        Rendering = Options.Create(new RenderingOptions());
    }
}
