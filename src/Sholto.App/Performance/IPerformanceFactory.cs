using Microsoft.Extensions.Options;
using Sholto.Data;
using Sholto.App.Decks;

namespace Sholto.App.Performance;

/// <summary>Builds the <see cref="PerformanceStack"/>.</summary>
public interface IPerformanceFactory
{
    PerformanceStack Build(IDecks decks, IPlaybackRequests playback,
        IOptions<ScratchOptions> scratchOptions, IOptions<MagnetismOptions> magnetismOptions, IFrameClock clock,
        IBackspinFeel backspinFeel);
}
