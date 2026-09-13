using Microsoft.Extensions.Options;
using Sholto.Controller.Mappings;

namespace Sholto.App;

/// <summary>One place that holds the app's configured options objects, so every
/// composition root — the real <c>App</c> and <c>tools/Sholto.Bench</c>'s
/// composers — constructs the same defaults instead of each declaring its own
/// <c>new XOptions()</c>.
///
/// <para>Bench already references <c>Sholto.App</c>, which already references
/// <c>Sholto.Controller</c>, so all four options types here are visible from
/// Bench today: no new project is needed to route Bench's construction sites
/// through <see cref="Default"/>. The one condition that would change that: if
/// <c>Sholto.Audio</c> or <c>Sholto.Controller</c> ever needed to READ configured
/// values, they could not reference <c>Sholto.App</c> without inverting the
/// onion. Not true today.</para></summary>
public sealed class SholtoOptions
{
    public IOptions<FeatureOptions> Feature { get; }
    public IOptions<DdjFlx4Options> DdjFlx4 { get; }
    public IOptions<ScratchOptions> Scratch { get; }
    public IOptions<MagnetismOptions> Magnetism { get; }

    private SholtoOptions(
        IOptions<FeatureOptions> feature,
        IOptions<DdjFlx4Options> ddjFlx4,
        IOptions<ScratchOptions> scratch,
        IOptions<MagnetismOptions> magnetism)
    {
        Feature = feature;
        DdjFlx4 = ddjFlx4;
        Scratch = scratch;
        Magnetism = magnetism;
    }

    /// <summary>The app's configured options, at their defaults. Every composition
    /// root (App, Bench) should read options through here rather than constructing
    /// its own — see the type doc above for why that matters.</summary>
    public static SholtoOptions Default { get; } = new(
        Options.Create(new FeatureOptions()),
        Options.Create(new DdjFlx4Options()),
        Options.Create(new ScratchOptions()),
        Options.Create(new MagnetismOptions()));
}
