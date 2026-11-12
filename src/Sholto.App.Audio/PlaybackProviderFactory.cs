using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Stems;

namespace Sholto.App.Audio;

/// <summary>Default <see cref="IPlaybackProviderFactory"/>. Called on track load,
/// never from an audio callback.</summary>
public sealed class PlaybackProviderFactory : IPlaybackProviderFactory
{
    private readonly IOptions<LoopOptions> _loopOptions;
    private readonly IOptions<VarispeedOptions> _varispeedOptions;

    /// <summary>Factory whose providers read at the default <see cref="VarispeedOptions"/>.</summary>
    public PlaybackProviderFactory(IOptions<LoopOptions> loopOptions)
        : this(loopOptions, Options.Create(new VarispeedOptions()))
    {
    }

    public PlaybackProviderFactory(IOptions<LoopOptions> loopOptions, IOptions<VarispeedOptions> varispeedOptions)
    {
        _loopOptions = loopOptions;
        _varispeedOptions = varispeedOptions;
    }

    public ScratchDataProvider Scratch(float[] stereoSamples, int sampleRate) =>
        new(stereoSamples, sampleRate, _varispeedOptions);

    public StemMixDataProvider Stems(StemSamples stemSamples, int sampleRate) =>
        new(stemSamples, sampleRate, _loopOptions, _varispeedOptions);
}
