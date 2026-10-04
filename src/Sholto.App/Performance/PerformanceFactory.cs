using Microsoft.Extensions.Options;
using Sholto.Data;
using Sholto.App.Decks;

namespace Sholto.App.Performance;

/// <summary>Builds the performance buckets: one <see cref="JogRecency"/> shared by the jog paths (writer)
/// and the scrub flag and magnet (reader).</summary>
public sealed class PerformanceFactory : IPerformanceFactory
{
    public PerformanceStack Build(IDecks decks, IPlaybackRequests playback,
        IOptions<ScratchOptions> scratchOptions, IOptions<MagnetismOptions> magnetismOptions, IFrameClock clock)
    {
        var recency = new JogRecency(clock, scratchOptions);
        var seek = new JogSeek(decks, recency);
        var scratch = new ScratchEngine(decks, playback, recency, clock, scratchOptions);
        var magnet = new MagnetSnap(decks, recency, clock, magnetismOptions);
        var platter = new Platter(decks, scratch, seek, recency, scratchOptions);
        var tick = new PerformanceTick(clock, decks, magnet, seek, scratch);
        return new PerformanceStack(platter, scratch, magnet, tick);
    }
}
