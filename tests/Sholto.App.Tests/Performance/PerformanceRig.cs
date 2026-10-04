using Microsoft.Extensions.Options;
using Sholto.Data;
using Sholto.App;
using Sholto.App.Performance;

namespace Sholto.App.Tests;

/// <summary>The performance buckets over a real headless core (<see cref="HeadlessCore"/>) and a manual clock, with each
/// bucket reachable by its public surface (the production factory hides the shared jog recency).</summary>
internal sealed class PerformanceRig
{
    public CoreStack Core { get; }
    public ManualFrameClock Clock { get; } = new();
    public JogRecency Recency { get; }
    public JogSeek Seek { get; }
    public ScratchEngine Scratch { get; }
    public MagnetSnap Magnet { get; }
    public Platter Platter { get; }
    public PerformanceTick Tick { get; }

    public PerformanceRig()
    {
        Core = new HeadlessCore().Core;
        var scratchOptions = Options.Create(new ScratchOptions());
        Recency = new JogRecency(Clock, scratchOptions);
        Seek = new JogSeek(Core.Decks, Recency);
        Scratch = new ScratchEngine(Core.Decks, Core.Playback, Recency, Clock, scratchOptions);
        Magnet = new MagnetSnap(Core.Decks, Recency, Clock, Options.Create(new MagnetismOptions()));
        Platter = new Platter(Core.Decks, Scratch, Seek, Recency, scratchOptions);
        Tick = new PerformanceTick(Clock, Core.Decks, Magnet, Seek, Scratch);
    }
}
